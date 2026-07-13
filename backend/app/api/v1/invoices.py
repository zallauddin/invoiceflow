"""Invoice API routes — tenant-scoped."""

from uuid import UUID

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import select, func
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.api.deps import get_current_user, CurrentUser
from app.models.invoice import Invoice, InvoiceLine
from app.schemas.invoice import (
    InvoiceCreate,
    InvoiceUpdate,
    InvoiceResponse,
    InvoiceListResponse,
)

router = APIRouter()


@router.get("/", response_model=InvoiceListResponse)
async def list_invoices(
    page: int = Query(1, ge=1),
    page_size: int = Query(50, ge=1, le=100),
    status_filter: str | None = Query(None, alias="status"),
    country: str | None = None,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """List invoices for the current tenant with pagination and filters."""
    query = select(Invoice).where(Invoice.tenant_id == current_user.tenant_id)

    if status_filter:
        query = query.where(Invoice.status == status_filter)
    if country:
        query = query.where(Invoice.country_code == country)

    # Count total
    count_query = select(func.count()).select_from(query.subquery())
    total = (await db.execute(count_query)).scalar() or 0

    # Paginate
    query = query.order_by(Invoice.created_at.desc())
    query = query.offset((page - 1) * page_size).limit(page_size)

    result = await db.execute(query)
    invoices = result.scalars().all()

    return InvoiceListResponse(
        invoices=[InvoiceResponse.model_validate(inv) for inv in invoices],
        total=total,
        page=page,
        page_size=page_size,
    )


@router.get("/{invoice_id}", response_model=InvoiceResponse)
async def get_invoice(
    invoice_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Get invoice by ID (tenant-scoped)."""
    result = await db.execute(
        select(Invoice).where(
            Invoice.id == invoice_id,
            Invoice.tenant_id == current_user.tenant_id,
        )
    )
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")
    return InvoiceResponse.model_validate(invoice)


@router.post("/", response_model=InvoiceResponse, status_code=status.HTTP_201_CREATED)
async def create_invoice(
    body: InvoiceCreate,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Create a new invoice (manual entry)."""
    invoice = Invoice(
        tenant_id=current_user.tenant_id,
        invoice_number=body.invoice_number,
        vendor_name=body.vendor_name,
        vendor_tax_id=body.vendor_tax_id,
        buyer_name=body.buyer_name,
        buyer_tax_id=body.buyer_tax_id,
        invoice_date=body.invoice_date,
        due_date=body.due_date,
        currency=body.currency,
        country_code=body.country_code,
        compliance_model=body.compliance_model,
        source="manual",
    )
    db.add(invoice)
    await db.flush()

    # Add lines
    for line_data in body.lines:
        line_total = line_data.quantity * line_data.unit_price
        tax_amount = line_total * (line_data.tax_rate / 100)
        line = InvoiceLine(
            invoice_id=invoice.id,
            line_number=line_data.line_number,
            description=line_data.description,
            quantity=line_data.quantity,
            unit_price=line_data.unit_price,
            tax_rate=line_data.tax_rate,
            tax_amount=tax_amount,
            line_total=line_total + tax_amount,
            item_code=line_data.item_code,
        )
        db.add(line)

    await db.flush()

    # Calculate totals
    subtotal = sum(l.quantity * l.unit_price for l in body.lines)
    tax = sum(l.quantity * l.unit_price * (l.tax_rate / 100) for l in body.lines)
    invoice.subtotal = subtotal
    invoice.tax_amount = tax
    invoice.total_amount = subtotal + tax

    return InvoiceResponse.model_validate(invoice)


@router.put("/{invoice_id}", response_model=InvoiceResponse)
async def update_invoice(
    invoice_id: UUID,
    body: InvoiceUpdate,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Update an existing invoice."""
    result = await db.execute(
        select(Invoice).where(
            Invoice.id == invoice_id,
            Invoice.tenant_id == current_user.tenant_id,
        )
    )
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    update_data = body.model_dump(exclude_unset=True)
    for field, value in update_data.items():
        if field != "lines":
            setattr(invoice, field, value)

    return InvoiceResponse.model_validate(invoice)


@router.delete("/{invoice_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_invoice(
    invoice_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Delete an invoice."""
    result = await db.execute(
        select(Invoice).where(
            Invoice.id == invoice_id,
            Invoice.tenant_id == current_user.tenant_id,
        )
    )
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    await db.delete(invoice)


@router.post("/{invoice_id}/approve", response_model=InvoiceResponse)
async def approve_invoice(
    invoice_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Approve an invoice for compliance processing."""
    result = await db.execute(
        select(Invoice).where(
            Invoice.id == invoice_id,
            Invoice.tenant_id == current_user.tenant_id,
        )
    )
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    if invoice.status not in ("extracted", "reviewing"):
        raise HTTPException(
            status_code=400,
            detail=f"Cannot approve invoice in '{invoice.status}' status",
        )

    invoice.status = "approved"
    return InvoiceResponse.model_validate(invoice)


@router.post("/{invoice_id}/reject", response_model=InvoiceResponse)
async def reject_invoice(
    invoice_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Reject an invoice."""
    result = await db.execute(
        select(Invoice).where(
            Invoice.id == invoice_id,
            Invoice.tenant_id == current_user.tenant_id,
        )
    )
    invoice = result.scalar_one_or_none()
    if not invoice:
        raise HTTPException(status_code=404, detail="Invoice not found")

    invoice.status = "rejected"
    return InvoiceResponse.model_validate(invoice)
