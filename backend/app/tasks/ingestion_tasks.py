"""Ingestion Celery tasks — async polling and processing for all channels."""

import asyncio
import base64
import logging
import uuid

from app.tasks.celery_app import celery_app
from app.database import async_session
from app.models.invoice import IngestionSource
from app.ingestion.email import EmailIngestion
from app.ingestion.ftp import FTPIngestion, SFTPIngestion
from app.ingestion.base import IngestedDocument
from app.ingestion.processor import batch_process_documents

logger = logging.getLogger(__name__)


def _trigger_extraction(invoices: list, tenant_id: str):
    """Queue extraction tasks for newly ingested invoices."""
    from app.tasks.extraction_tasks import extract_invoice
    for inv in invoices:
        try:
            extract_invoice.delay(str(inv.id), tenant_id)
        except Exception as e:
            logger.error(f"Failed to queue extraction for {inv.id}: {e}")


@celery_app.task(bind=True, max_retries=3, default_retry_delay=60)
def poll_email_inbox(self, tenant_id: str):
    """Poll email inbox for new invoice attachments.

    Scheduled via Celery Beat or triggered manually.
    """
    async def _poll():
        tenant_uuid = uuid.UUID(tenant_id)
        async with async_session() as db:
            try:
                ingestion = EmailIngestion(tenant_id)
                documents = await ingestion.poll()

                if documents:
                    invoices = await batch_process_documents(
                        db, tenant_uuid, documents, IngestionSource.EMAIL
                    )
                    _trigger_extraction(invoices, tenant_id)
                    logger.info(
                        f"Email polling complete: {len(invoices)} invoices created, "
                        f"extraction queued"
                    )
                    return {"processed": len(invoices), "total_found": len(documents)}
                return {"processed": 0, "total_found": 0}

            except Exception as e:
                logger.error(f"Email polling failed: {e}", exc_info=True)
                raise self.retry(exc=e)

    return asyncio.run(_poll())


@celery_app.task(bind=True, max_retries=3, default_retry_delay=60)
def poll_ftp_folder(self, tenant_id: str):
    """Poll FTP server for new invoice files."""
    async def _poll():
        tenant_uuid = uuid.UUID(tenant_id)
        async with async_session() as db:
            try:
                ingestion = FTPIngestion(tenant_id)
                documents = await ingestion.poll()

                if documents:
                    invoices = await batch_process_documents(
                        db, tenant_uuid, documents, IngestionSource.FTP
                    )
                    _trigger_extraction(invoices, tenant_id)
                    logger.info(
                        f"FTP polling complete: {len(invoices)} invoices created, "
                        f"extraction queued"
                    )
                    return {"processed": len(invoices), "total_found": len(documents)}
                return {"processed": 0, "total_found": 0}

            except Exception as e:
                logger.error(f"FTP polling failed: {e}", exc_info=True)
                raise self.retry(exc=e)

    return asyncio.run(_poll())


@celery_app.task(bind=True, max_retries=3, default_retry_delay=60)
def poll_sftp_folder(self, tenant_id: str):
    """Poll SFTP server for new invoice files."""
    async def _poll():
        tenant_uuid = uuid.UUID(tenant_id)
        async with async_session() as db:
            try:
                ingestion = SFTPIngestion(tenant_id)
                documents = await ingestion.poll()

                if documents:
                    invoices = await batch_process_documents(
                        db, tenant_uuid, documents, IngestionSource.SFTP
                    )
                    _trigger_extraction(invoices, tenant_id)
                    logger.info(
                        f"SFTP polling complete: {len(invoices)} invoices created, "
                        f"extraction queued"
                    )
                    return {"processed": len(invoices), "total_found": len(documents)}
                return {"processed": 0, "total_found": 0}

            except Exception as e:
                logger.error(f"SFTP polling failed: {e}", exc_info=True)
                raise self.retry(exc=e)

    return asyncio.run(_poll())


@celery_app.task(bind=True, max_retries=3, default_retry_delay=30)
def process_uploaded_file(
    self, tenant_id: str, file_content_b64: str, filename: str, mime_type: str
):
    """Process a file uploaded via the REST API.

    Receives base64-encoded file content, decodes, stores, and creates invoice.
    Then queues extraction.
    """
    async def _process():
        tenant_uuid = uuid.UUID(tenant_id)
        async with async_session() as db:
            try:
                content = base64.b64decode(file_content_b64)
                document = IngestedDocument(
                    filename=filename,
                    content=content,
                    mime_type=mime_type,
                    source_reference=f"api-upload-{uuid.uuid4().hex[:12]}",
                )

                invoices = await batch_process_documents(
                    db, tenant_uuid, [document], IngestionSource.API
                )

                if invoices:
                    _trigger_extraction(invoices, tenant_id)
                    return {
                        "invoice_id": str(invoices[0].id),
                        "status": "received",
                        "filename": filename,
                    }
                return {"error": "Failed to process document"}

            except Exception as e:
                logger.error(f"Upload processing failed: {e}", exc_info=True)
                raise self.retry(exc=e)

    return asyncio.run(_process())


@celery_app.task(bind=True, max_retries=3, default_retry_delay=30)
def process_webhook_file(
    self, tenant_id: str, file_content_b64: str, filename: str,
    mime_type: str, source_reference: str = "",
):
    """Process a file received via webhook (external system push)."""
    async def _process():
        tenant_uuid = uuid.UUID(tenant_id)
        async with async_session() as db:
            try:
                content = base64.b64decode(file_content_b64)
                document = IngestedDocument(
                    filename=filename,
                    content=content,
                    mime_type=mime_type,
                    source_reference=source_reference or f"webhook-{uuid.uuid4().hex[:12]}",
                )

                invoices = await batch_process_documents(
                    db, tenant_uuid, [document], IngestionSource.WEBHOOK
                )

                if invoices:
                    _trigger_extraction(invoices, tenant_id)
                    return {
                        "invoice_id": str(invoices[0].id),
                        "status": "received",
                        "filename": filename,
                    }
                return {"error": "Failed to process document"}

            except Exception as e:
                logger.error(f"Webhook processing failed: {e}", exc_info=True)
                raise self.retry(exc=e)

    return asyncio.run(_process())
