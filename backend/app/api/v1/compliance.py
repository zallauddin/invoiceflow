"""Compliance API routes.

Provides endpoints for:
- Compliance status overview
- Invoice validation (pre-check)
- Compliance processing trigger
- Transmission trigger
- Archival trigger
- Compliance configuration
"""

from uuid import UUID

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select, func
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.api.deps import get_current_user, CurrentUser
from app.models.invoice import Invoice, InvoiceStatus, ComplianceModel
from app.models.compliance import ComplianceConfig
from app.models.audit import AuditLog
from app.schemas.compliance import (
    ComplianceConfigCreate,
    ComplianceConfigResponse,
    ComplianceStatusResponse,
    ComplianceValidationResponse,
)
from app.tasks.compliance_tasks import process_compliance, process_compliance_batch, transmit_invoice, archive_invoice

router = APIRouter()


@router.get("/status", response_model=ComplianceStatusResponse)
async def compliance_status(
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Get compliance status summary for the tenant."""
    tenant_id = current_user.tenant_id

    # Count by status
    stmt = select(Invoice.status, func.count(Invoice.id)).where(
        Invoice.tenant_id == tenant_id
    ).group_by(Invoice.status)
    result = await db.execute(stmt)
    status_counts = {row[0]: row[1] for row in result.all()}

    pending = sum(status_counts.get(s, 0) for s in [InvoiceStatus.EXTRACTED, InvoiceStatus.APPROVED])
    compliant = status_counts.get(InvoiceStatus.COMPLIANT, 0) + status_counts.get(InvoiceStatus.TRANSMITTED, 0)
    failed = status_counts.get(InvoiceStatus.FAILED, 0)

    return ComplianceStatusResponse(pending=pending, compliant=compliant, failed=failed)


@router.post("/validate/{invoice_id}", response_model=ComplianceValidationResponse)
async def validate_compliance(
    invoice_id: str,
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Validate an invoice against compliance rules (pre-check, no submission)."""
    tenant_id = current_user.tenant_id

    # Fetch invoice
    stmt = select(Invoice).where(
        Invoice.id == invoice_id, Invoice.tenant_id == tenant_id
    )
    result = await db.execute(stmt)
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    # Build invoice data
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
        "vendor_name": invoice.vendor_name,
        "vendor_tax_id": invoice.vendor_tax_id or "",
        "buyer_name": invoice.buyer_name or "",
        "buyer_tax_id": invoice.buyer_tax_id or "",
        "currency": invoice.currency,
        "subtotal": invoice.subtotal,
        "tax_amount": invoice.tax_amount,
        "total_amount": invoice.total_amount,
        "country_code": invoice.country_code,
        "extra_fields": {},
    }

    from app.compliance.engine import ComplianceEngine
    engine = ComplianceEngine(sandbox=True)
    result = await engine.validate_only(
        invoice_data=invoice_data,
        compliance_model=invoice.compliance_model.value if invoice.compliance_model else "peppol",
        country_code=invoice.country_code,
        lines=lines_data,
    )

    return ComplianceValidationResponse(
        valid=result.success,
        errors=result.errors,
        warnings=result.warnings,
    )


@router.post("/process/{invoice_id}")
async def trigger_compliance(
    invoice_id: str,
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Trigger compliance processing for an invoice."""
    tenant_id = current_user.tenant_id

    # Verify invoice exists and is in valid state
    stmt = select(Invoice).where(
        Invoice.id == invoice_id, Invoice.tenant_id == tenant_id
    )
    result = await db.execute(stmt)
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    if invoice.status not in (InvoiceStatus.EXTRACTED, InvoiceStatus.APPROVED):
        raise HTTPException(
            status_code=400,
            detail=f"Invoice must be in EXTRACTED or APPROVED status, got {invoice.status.value}",
        )

    # Queue compliance task
    task = process_compliance.delay(invoice_id=str(invoice_id), tenant_id=str(tenant_id))

    # Audit log
    audit = AuditLog(
        tenant_id=tenant_id,
        invoice_id=invoice_id,
        action="compliance_triggered",
        details={"task_id": task.id, "model": invoice.compliance_model.value if invoice.compliance_model else "peppol"},
    )
    db.add(audit)
    await db.commit()

    return {"task_id": task.id, "status": "queued", "invoice_id": invoice_id}


@router.post("/process-batch")
async def trigger_batch_compliance(
    invoice_ids: list[str],
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Trigger compliance processing for multiple invoices."""
    if len(invoice_ids) > 100:
        raise HTTPException(status_code=400, detail="Maximum batch size is 100 invoices")

    tenant_id = current_user.tenant_id

    task = process_compliance_batch.delay(
        invoice_ids=invoice_ids,
        tenant_id=str(tenant_id),
    )

    return {"task_id": task.id, "status": "queued", "invoice_count": len(invoice_ids)}


@router.post("/transmit/{invoice_id}")
async def trigger_transmission(
    invoice_id: str,
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Transmit a compliant invoice to the network."""
    tenant_id = current_user.tenant_id

    stmt = select(Invoice).where(
        Invoice.id == invoice_id, Invoice.tenant_id == tenant_id
    )
    result = await db.execute(stmt)
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    if invoice.status != InvoiceStatus.COMPLIANT:
        raise HTTPException(
            status_code=400,
            detail=f"Invoice must be COMPLIANT before transmission, got {invoice.status.value}",
        )

    task = transmit_invoice.delay(invoice_id=str(invoice_id), tenant_id=str(tenant_id))

    return {"task_id": task.id, "status": "transmitting", "invoice_id": invoice_id}


@router.post("/archive/{invoice_id}")
async def trigger_archive(
    invoice_id: str,
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Archive an invoice for post-audit compliance."""
    tenant_id = current_user.tenant_id

    stmt = select(Invoice).where(
        Invoice.id == invoice_id, Invoice.tenant_id == tenant_id
    )
    result = await db.execute(stmt)
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    task = archive_invoice.delay(invoice_id=str(invoice_id), tenant_id=str(tenant_id))

    return {"task_id": task.id, "status": "archiving", "invoice_id": invoice_id}


@router.get("/config", response_model=list[ComplianceConfigResponse])
async def list_compliance_configs(
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """List all compliance configurations for the tenant."""
    tenant_id = current_user.tenant_id
    stmt = select(ComplianceConfig).where(ComplianceConfig.tenant_id == tenant_id)
    result = await db.execute(stmt)
    configs = result.scalars().all()
    return configs


@router.post("/config", response_model=ComplianceConfigResponse)
async def create_compliance_config(
    config_data: ComplianceConfigCreate,
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Create a new compliance configuration."""
    tenant_id = current_user.tenant_id

    config = ComplianceConfig(
        tenant_id=tenant_id,
        country_code=config_data.country_code,
        model=config_data.model,
        enabled=config_data.enabled,
        config=config_data.config,
    )
    db.add(config)
    await db.commit()
    await db.refresh(config)

    return config


@router.get("/task/{task_id}")
async def check_compliance_task(
    task_id: str,
    current_user: CurrentUser = Depends(get_current_user),
):
    """Check status of a compliance Celery task."""
    from app.tasks.celery_app import celery_app
    from celery.result import AsyncResult

    result = AsyncResult(task_id, app=celery_app)
    response = {
        "task_id": task_id,
        "status": result.status,
        "result": None,
    }

    if result.ready():
        response["result"] = result.result
    elif result.failed():
        response["result"] = {"error": str(result.info)}

    return response
