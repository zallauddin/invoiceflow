"""Analytics API routes."""

from datetime import datetime, timedelta

from fastapi import APIRouter, Depends
from sqlalchemy import select, func
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.api.deps import get_current_user, CurrentUser
from app.models.invoice import Invoice, InvoiceStatus

router = APIRouter()


@router.get("/dashboard")
async def dashboard_stats(
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Get dashboard statistics for the current tenant."""
    tenant_id = current_user.tenant_id
    today = datetime.utcnow().replace(hour=0, minute=0, second=0, microsecond=0)

    # Invoices today
    today_result = await db.execute(
        select(func.count(Invoice.id)).where(
            Invoice.tenant_id == tenant_id,
            Invoice.created_at >= today,
        )
    )
    invoices_today = today_result.scalar() or 0

    # Success rate (compliant + transmitted / total processed)
    total_processed_result = await db.execute(
        select(func.count(Invoice.id)).where(
            Invoice.tenant_id == tenant_id,
            Invoice.status.in_([
                InvoiceStatus.COMPLIANT,
                InvoiceStatus.TRANSMITTED,
                InvoiceStatus.FAILED,
                InvoiceStatus.REJECTED,
            ]),
        )
    )
    total_processed = total_processed_result.scalar() or 0

    success_result = await db.execute(
        select(func.count(Invoice.id)).where(
            Invoice.tenant_id == tenant_id,
            Invoice.status.in_([InvoiceStatus.COMPLIANT, InvoiceStatus.TRANSMITTED]),
        )
    )
    success_count = success_result.scalar() or 0
    success_rate = round((success_count / total_processed * 100), 1) if total_processed > 0 else 0.0

    # Pending
    pending_result = await db.execute(
        select(func.count(Invoice.id)).where(
            Invoice.tenant_id == tenant_id,
            Invoice.status.in_([InvoiceStatus.RECEIVED, InvoiceStatus.PROCESSING, InvoiceStatus.EXTRACTED, InvoiceStatus.REVIEWING, InvoiceStatus.APPROVED]),
        )
    )
    pending = pending_result.scalar() or 0

    # Total processed (all time)
    total_all_result = await db.execute(
        select(func.count(Invoice.id)).where(Invoice.tenant_id == tenant_id)
    )
    total_all = total_all_result.scalar() or 0

    return {
        "invoices_today": invoices_today,
        "success_rate": success_rate,
        "pending": pending,
        "total_processed": total_all,
    }


@router.get("/charts/daily")
async def daily_volume(
    days: int = 7,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Get daily invoice volume for charts."""
    tenant_id = current_user.tenant_id
    labels = []
    data = []

    for i in range(days - 1, -1, -1):
        day = datetime.utcnow().replace(hour=0, minute=0, second=0, microsecond=0) - timedelta(days=i)
        next_day = day + timedelta(days=1)

        count_result = await db.execute(
            select(func.count(Invoice.id)).where(
                Invoice.tenant_id == tenant_id,
                Invoice.created_at >= day,
                Invoice.created_at < next_day,
            )
        )
        count = count_result.scalar() or 0

        labels.append(day.strftime("%a %d"))
        data.append(count)

    return {"labels": labels, "data": data}


@router.get("/charts/by-country")
async def by_country(
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Get invoice counts by country."""
    tenant_id = current_user.tenant_id

    result = await db.execute(
        select(Invoice.country_code, func.count(Invoice.id))
        .where(Invoice.tenant_id == tenant_id)
        .group_by(Invoice.country_code)
        .order_by(func.count(Invoice.id).desc())
    )
    rows = result.all()

    return {
        "labels": [row[0] for row in rows],
        "data": [row[1] for row in rows],
    }
