"""Compliance processing Celery tasks."""

import asyncio
import logging
from datetime import datetime

from app.tasks.celery_app import celery_app
from app.database import async_session
from app.models.invoice import Invoice, InvoiceStatus
from app.models.audit import AuditLog
from app.config import settings

logger = logging.getLogger(__name__)


@celery_app.task(bind=True, max_retries=3, default_retry_delay=30)
def process_compliance(self, invoice_id: str, tenant_id: str):
    """
    Process a single invoice through the compliance engine.
    Determines compliance model from invoice data and routes to the correct handler.
    """
    logger.info(f"Processing compliance for invoice {invoice_id}")
    return asyncio.run(_process_compliance_async(self, invoice_id, tenant_id))


@celery_app.task(bind=True, max_retries=2, default_retry_delay=60)
def process_compliance_batch(self, invoice_ids: list[str], tenant_id: str):
    """Process a batch of invoices through compliance."""
    logger.info(f"Processing compliance batch: {len(invoice_ids)} invoices")
    results = []
    for invoice_id in invoice_ids:
        try:
            result = asyncio.run(_process_compliance_async(self, invoice_id, tenant_id))
            results.append(result)
        except Exception as e:
            logger.error(f"Failed compliance for {invoice_id}: {e}")
            results.append({"invoice_id": invoice_id, "success": False, "error": str(e)})
    return results


@celery_app.task(bind=True, max_retries=2, default_retry_delay=60)
def transmit_invoice(self, invoice_id: str, tenant_id: str):
    """Transmit a compliant invoice to the network."""
    logger.info(f"Transmitting invoice {invoice_id}")
    return asyncio.run(_transmit_async(self, invoice_id, tenant_id))


@celery_app.task(bind=True, max_retries=1, default_retry_delay=120)
def archive_invoice(self, invoice_id: str, tenant_id: str):
    """Archive an invoice for post-audit compliance."""
    logger.info(f"Archiving invoice {invoice_id}")
    return asyncio.run(_archive_async(self, invoice_id, tenant_id))


async def _process_compliance_async(task, invoice_id: str, tenant_id: str) -> dict:
    """Async compliance processing."""
    from app.compliance.engine import ComplianceEngine

    async with async_session() as session:
        # Fetch invoice
        from sqlalchemy import select
        stmt = select(Invoice).where(Invoice.id == invoice_id)
        result = await session.execute(stmt)
        invoice = result.scalar_one_or_none()

        if not invoice:
            return {"invoice_id": invoice_id, "success": False, "error": "Invoice not found"}

        # Only process extracted/approved invoices
        if invoice.status not in (InvoiceStatus.EXTRACTED, InvoiceStatus.APPROVED):
            return {"invoice_id": invoice_id, "success": False, "error": f"Invalid status: {invoice.status}"}

        # Build invoice data for compliance engine
        lines_data = []
        for line in invoice.lines:
            lines_data.append({
                "description": line.description,
                "quantity": line.quantity,
                "unit_price": line.unit_price,
                "tax_rate": line.tax_rate,
                "tax_amount": line.tax_amount,
                "line_total": line.line_total,
                "item_code": line.item_code,
            })

        invoice_data = {
            "invoice_number": invoice.invoice_number,
            "invoice_date": invoice.invoice_date.isoformat() if invoice.invoice_date else "",
            "due_date": invoice.due_date.isoformat() if invoice.due_date else "",
            "vendor_name": invoice.vendor_name,
            "vendor_tax_id": invoice.vendor_tax_id or "",
            "buyer_name": invoice.buyer_name or "",
            "buyer_tax_id": invoice.buyer_tax_id or "",
            "currency": invoice.currency,
            "subtotal": invoice.subtotal,
            "tax_amount": invoice.tax_amount,
            "total_amount": invoice.total_amount,
            "country_code": invoice.country_code,
            "invoice_type_code": "380",
            "extra_fields": {},
        }

        # Run compliance engine (respect sandbox/production setting)
        engine = ComplianceEngine(sandbox=settings.COMPLIANCE_SANDBOX)
        compliance_result = await engine.process(
            invoice_data=invoice_data,
            compliance_model=invoice.compliance_model.value if invoice.compliance_model else "peppol",
            country_code=invoice.country_code,
            lines=lines_data,
        )

        # Update invoice status
        if compliance_result.success:
            invoice.status = InvoiceStatus.COMPLIANT
            invoice.compliance_response = compliance_result.transmission
        else:
            invoice.status = InvoiceStatus.FAILED
            invoice.error_message = "; ".join(compliance_result.errors)

        invoice.updated_at = datetime.utcnow()

        # Create audit log
        audit = AuditLog(
            tenant_id=tenant_id,
            invoice_id=invoice_id,
            action="compliance_processed",
            details={
                "model": compliance_result.model,
                "country_code": compliance_result.country_code,
                "status": compliance_result.status,
                "success": compliance_result.success,
                "clearance_id": compliance_result.clearance_id,
            },
            message="; ".join(compliance_result.errors) if compliance_result.errors else f"Status: {compliance_result.status}",
        )
        session.add(audit)
        await session.commit()

        return {
            "invoice_id": invoice_id,
            "success": compliance_result.success,
            "status": compliance_result.status,
            "clearance_id": compliance_result.clearance_id,
            "errors": compliance_result.errors,
        }


async def _transmit_async(task, invoice_id: str, tenant_id: str) -> dict:
    """Async transmission of a compliant invoice."""
    async with async_session() as session:
        from sqlalchemy import select
        stmt = select(Invoice).where(Invoice.id == invoice_id)
        result = await session.execute(stmt)
        invoice = result.scalar_one_or_none()

        if not invoice:
            return {"invoice_id": invoice_id, "success": False, "error": "Invoice not found"}

        if invoice.status != InvoiceStatus.COMPLIANT:
            return {"invoice_id": invoice_id, "success": False, "error": f"Must be COMPLIANT, got {invoice.status}"}

        # Mark as transmitted
        invoice.status = InvoiceStatus.TRANSMITTED
        invoice.updated_at = datetime.utcnow()

        audit = AuditLog(
            tenant_id=tenant_id,
            invoice_id=invoice_id,
            action="invoice_transmitted",
            details={"previous_status": "compliant", "new_status": "transmitted"},
        )
        session.add(audit)
        await session.commit()

        return {"invoice_id": invoice_id, "success": True, "status": "transmitted"}


async def _archive_async(task, invoice_id: str, tenant_id: str) -> dict:
    """Async archival for post-audit."""
    from app.compliance.post_audit import PostAuditArchiver, ArchiveRequest

    async with async_session() as session:
        from sqlalchemy import select
        stmt = select(Invoice).where(Invoice.id == invoice_id)
        result = await session.execute(stmt)
        invoice = result.scalar_one_or_none()

        if not invoice:
            return {"invoice_id": invoice_id, "success": False, "error": "Invoice not found"}

        archiver = PostAuditArchiver()
        request = ArchiveRequest(
            invoice_number=invoice.invoice_number,
            invoice_date=invoice.invoice_date.isoformat() if invoice.invoice_date else "",
            vendor_name=invoice.vendor_name,
            vendor_tax_id=invoice.vendor_tax_id or "",
            buyer_name=invoice.buyer_name or "",
            buyer_tax_id=invoice.buyer_tax_id or "",
            currency=invoice.currency,
            total_amount=invoice.total_amount,
            tax_amount=invoice.tax_amount,
            country_code=invoice.country_code,
        )

        archive_result = await archiver.archive(request)

        if archive_result.success:
            invoice.compliance_response = {
                **(invoice.compliance_response or {}),
                "archive_id": archive_result.archive_id,
                "archive_path": archive_result.archive_path,
                "retention_until": archive_result.retention_until,
            }
            invoice.updated_at = datetime.utcnow()

            audit = AuditLog(
                tenant_id=tenant_id,
                invoice_id=invoice_id,
                action="invoice_archived",
                details={"archive_id": archive_result.archive_id, "checksum": archive_result.checksum},
            )
            session.add(audit)
            await session.commit()

        return {
            "invoice_id": invoice_id,
            "success": archive_result.success,
            "archive_id": archive_result.archive_id,
            "errors": archive_result.errors,
        }
