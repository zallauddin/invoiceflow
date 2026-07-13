"""Extraction API — trigger and monitor AI extraction."""

import uuid

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.api.deps import CurrentUser, get_current_user
from app.models.invoice import Invoice, InvoiceStatus
from app.schemas.common import MessageResponse

router = APIRouter(prefix="/extraction", tags=["extraction"])


@router.post("/extract/{invoice_id}", response_model=MessageResponse)
async def trigger_extraction(
    invoice_id: uuid.UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Manually trigger extraction for an invoice.

    Useful for re-extraction or when auto-trigger was missed.
    """
    # Load invoice
    result = await db.execute(
        select(Invoice).where(
            Invoice.id == invoice_id,
            Invoice.tenant_id == current_user.tenant_id,
        )
    )
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    # Queue extraction
    from app.tasks.extraction_tasks import extract_invoice

    extract_invoice.delay(
        str(invoice.id), str(current_user.tenant_id)
    )

    return MessageResponse(
        message=f"Extraction queued for invoice {invoice.id}"
    )


@router.post("/extract-batch", response_model=MessageResponse)
async def trigger_batch_extraction(
    invoice_ids: list[uuid.UUID],
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Trigger extraction for multiple invoices at once."""
    if len(invoice_ids) > 100:
        raise HTTPException(
            status_code=400, detail="Maximum 100 invoices per batch"
        )

    from app.tasks.extraction_tasks import extract_batch

    extract_batch.delay(
        [str(i) for i in invoice_ids], str(current_user.tenant_id)
    )

    return MessageResponse(
        message=f"Batch extraction queued for {len(invoice_ids)} invoices"
    )


@router.get("/status/{invoice_id}")
async def get_extraction_status(
    invoice_id: uuid.UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Get extraction status for an invoice."""
    result = await db.execute(
        select(Invoice).where(
            Invoice.id == invoice_id,
            Invoice.tenant_id == current_user.tenant_id,
        )
    )
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    return {
        "invoice_id": str(invoice.id),
        "status": invoice.status.value,
        "extraction_method": (
            invoice.extraction_method.value if invoice.extraction_method else None
        ),
        "ocr_confidence": invoice.ocr_confidence,
        "error_message": invoice.error_message,
        "processing_started_at": (
            invoice.processing_started_at.isoformat()
            if invoice.processing_started_at else None
        ),
        "processing_completed_at": (
            invoice.processing_completed_at.isoformat()
            if invoice.processing_completed_at else None
        ),
    }
