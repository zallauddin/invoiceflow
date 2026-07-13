"""Celery tasks for AI extraction pipeline."""

import asyncio
import base64
import logging
import uuid

from app.tasks.celery_app import celery_app
from app.database import async_session
from app.extraction.pipeline import pipeline
from app.ingestion.storage import StorageManager
from app.models.invoice import Invoice

logger = logging.getLogger(__name__)

# Lazy-init storage
_storage: StorageManager | None = None


def _get_storage() -> StorageManager:
    global _storage
    if _storage is None:
        _storage = StorageManager()
    return _storage


@celery_app.task(
    name="extraction.extract_invoice",
    bind=True,
    max_retries=3,
    default_retry_delay=30,
)
def extract_invoice(self, invoice_id: str, tenant_id: str):
    """Extract data from a single invoice.

    Downloads file from MinIO, runs extraction pipeline,
    updates invoice record with extracted data.
    """
    logger.info(f"Starting extraction for invoice {invoice_id}")

    async def _run():
        async with async_session() as db:
            try:
                # Convert string IDs to UUID
                inv_id = uuid.UUID(invoice_id)
                ten_id = uuid.UUID(tenant_id)

                # Load invoice to get file info
                from sqlalchemy import select
                result = await db.execute(
                    select(Invoice).where(Invoice.id == inv_id)
                )
                invoice = result.scalar_one_or_none()
                if not invoice:
                    logger.error(f"Invoice {invoice_id} not found")
                    return {"status": "error", "error": "Invoice not found"}

                # Download file from storage
                storage = _get_storage()
                file_bytes = await storage.download_file(invoice.file_url)
                mime_type = invoice.mime_type or "application/pdf"

                # Run extraction pipeline
                updated_invoice = await pipeline.extract_invoice(
                    db=db,
                    invoice_id=inv_id,
                    file_bytes=file_bytes,
                    mime_type=mime_type,
                )

                await db.commit()

                logger.info(
                    f"Invoice {invoice_id} extraction complete: "
                    f"status={updated_invoice.status.value}, "
                    f"method={updated_invoice.extraction_method}"
                )

                return {
                    "status": "success",
                    "invoice_id": invoice_id,
                    "extraction_status": updated_invoice.status.value,
                    "extraction_method": (
                        updated_invoice.extraction_method.value
                        if updated_invoice.extraction_method
                        else None
                    ),
                    "confidence": updated_invoice.ocr_confidence,
                }

            except Exception as e:
                await db.rollback()
                logger.error(f"Extraction failed for invoice {invoice_id}: {e}")
                raise

    try:
        return asyncio.run(_run())
    except Exception as e:
        logger.error(f"Extraction task failed: {e}")
        # Retry on transient errors
        if "Connection" in str(e) or "timeout" in str(e).lower():
            raise self.retry(exc=e)
        return {"status": "error", "error": str(e)}


@celery_app.task(
    name="extraction.extract_batch",
    bind=True,
    max_retries=2,
    default_retry_delay=60,
)
def extract_batch(self, invoice_ids: list[str], tenant_id: str):
    """Extract data from multiple invoices in batch.

    Queues individual extraction tasks for each invoice.
    """
    logger.info(
        f"Queuing batch extraction for {len(invoice_ids)} invoices "
        f"(tenant={tenant_id})"
    )

    queued = 0
    for inv_id in invoice_ids:
        try:
            extract_invoice.delay(inv_id, tenant_id)
            queued += 1
        except Exception as e:
            logger.error(f"Failed to queue extraction for {inv_id}: {e}")

    return {
        "status": "success",
        "queued": queued,
        "total": len(invoice_ids),
        "tenant_id": tenant_id,
    }


@celery_app.task(
    name="extraction.retry_extraction",
    bind=True,
    max_retries=2,
    default_retry_delay=30,
)
def retry_extraction(self, invoice_id: str, tenant_id: str):
    """Retry extraction for a failed invoice.

    Resets the invoice status and re-runs extraction.
    """
    logger.info(f"Retrying extraction for invoice {invoice_id}")

    async def _run():
        async with async_session() as db:
            try:
                from sqlalchemy import select
                inv_id = uuid.UUID(invoice_id)

                result = await db.execute(
                    select(Invoice).where(Invoice.id == inv_id)
                )
                invoice = result.scalar_one_or_none()
                if not invoice:
                    return {"status": "error", "error": "Invoice not found"}

                # Reset status to RECEIVED for re-extraction
                from app.models.invoice import InvoiceStatus
                invoice.status = InvoiceStatus.RECEIVED
                invoice.error_message = None
                await db.commit()

            except Exception as e:
                await db.rollback()
                logger.error(f"Retry reset failed for {invoice_id}: {e}")
                raise

    try:
        asyncio.run(_run())
        # Re-queue extraction
        return extract_invoice.delay(invoice_id, tenant_id)
    except Exception as e:
        logger.error(f"Retry extraction task failed: {e}")
        return {"status": "error", "error": str(e)}
