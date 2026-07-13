"""ERP Connector API endpoints."""

from datetime import datetime
from typing import List
from uuid import UUID

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select, func
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.deps import get_current_user, CurrentUser
from app.config import settings
from app.database import get_db
from app.models.connector import ERPConnectorConfig, ConnectorStatus
from app.models.invoice import Invoice, InvoiceStatus
from app.schemas.connector import (
    ConnectorCreate,
    ConnectorResponse,
    ConnectorSyncRequest,
    ConnectorSyncResponse,
    ConnectorTestResponse,
    AvailableConnector,
)
from app.connectors import get_connector, list_connectors, ERPInvoice, SyncDirection

router = APIRouter()


@router.get("/available", response_model=List[AvailableConnector])
async def list_available_connectors(current_user: CurrentUser = Depends(get_current_user)):
    """List all available connector types."""
    return list_connectors()


@router.get("/", response_model=List[ConnectorResponse])
async def list_configured_connectors(
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """List configured connectors for the tenant."""
    result = await db.execute(
        select(ERPConnectorConfig)
        .where(ERPConnectorConfig.tenant_id == current_user.tenant_id)
        .order_by(ERPConnectorConfig.created_at.desc())
    )
    connectors = result.scalars().all()
    return connectors


@router.post("/", response_model=ConnectorResponse, status_code=201)
async def create_connector(
    data: ConnectorCreate,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Create a new ERP connector configuration."""
    # Check if connector type already exists for this tenant
    existing = await db.execute(
        select(ERPConnectorConfig).where(
            ERPConnectorConfig.tenant_id == current_user.tenant_id,
            ERPConnectorConfig.connector_type == data.connector_type,
        )
    )
    if existing.scalar_one_or_none():
        raise HTTPException(
            status_code=400,
            detail=f"Connector '{data.connector_type}' already configured for this tenant",
        )

    connector = ERPConnectorConfig(
        tenant_id=current_user.tenant_id,
        connector_type=data.connector_type,
        display_name=data.display_name,
        api_key=data.api_key,
        api_secret=data.api_secret,
        base_url=data.base_url,
        sandbox=data.sandbox,
        sync_direction=data.sync_direction,
        extra_config=data.extra_config or {},
        status=ConnectorStatus.INACTIVE,
    )
    db.add(connector)
    await db.commit()
    await db.refresh(connector)
    return connector


@router.get("/{connector_id}", response_model=ConnectorResponse)
async def get_connector_config(
    connector_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Get a specific connector configuration."""
    result = await db.execute(
        select(ERPConnectorConfig).where(
            ERPConnectorConfig.id == connector_id,
            ERPConnectorConfig.tenant_id == current_user.tenant_id,
        )
    )
    connector = result.scalar_one_or_none()
    if not connector:
        raise HTTPException(status_code=404, detail="Connector not found")
    return connector


@router.delete("/{connector_id}", status_code=204)
async def delete_connector(
    connector_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Delete an ERP connector configuration."""
    result = await db.execute(
        select(ERPConnectorConfig).where(
            ERPConnectorConfig.id == connector_id,
            ERPConnectorConfig.tenant_id == current_user.tenant_id,
        )
    )
    connector = result.scalar_one_or_none()
    if not connector:
        raise HTTPException(status_code=404, detail="Connector not found")

    await db.delete(connector)
    await db.commit()


@router.post("/{connector_id}/test", response_model=ConnectorTestResponse)
async def test_connector(
    connector_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Test connection to the ERP system."""
    result = await db.execute(
        select(ERPConnectorConfig).where(
            ERPConnectorConfig.id == connector_id,
            ERPConnectorConfig.tenant_id == current_user.tenant_id,
        )
    )
    config = result.scalar_one_or_none()
    if not config:
        raise HTTPException(status_code=404, detail="Connector not found")

    connector = get_connector(
        connector_type=config.connector_type,
        api_key=config.api_key or "",
        api_secret=config.api_secret or "",
        base_url=config.base_url or "",
        sandbox=config.sandbox,
        tenant_id=str(current_user.tenant_id),
        extra_config=config.extra_config,
    )

    # For Xero, set stored tokens
    if config.connector_type == "xero" and config.access_token:
        connector._access_token = config.access_token
        connector._refresh_token = config.refresh_token
        connector._token_expiry = config.token_expiry

    result = await connector.test_connection()
    return result


@router.post("/{connector_id}/sync", response_model=ConnectorSyncResponse)
async def sync_connector(
    connector_id: UUID,
    sync_req: ConnectorSyncRequest,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Sync invoices with the ERP system (push or pull)."""
    result = await db.execute(
        select(ERPConnectorConfig).where(
            ERPConnectorConfig.id == connector_id,
            ERPConnectorConfig.tenant_id == current_user.tenant_id,
        )
    )
    config = result.scalar_one_or_none()
    if not config:
        raise HTTPException(status_code=404, detail="Connector not found")

    connector = get_connector(
        connector_type=config.connector_type,
        api_key=config.api_key or "",
        api_secret=config.api_secret or "",
        base_url=config.base_url or "",
        sandbox=config.sandbox,
        tenant_id=str(current_user.tenant_id),
        extra_config=config.extra_config,
    )

    # For Xero, set stored tokens
    if config.connector_type == "xero" and config.access_token:
        connector._access_token = config.access_token
        connector._refresh_token = config.refresh_token
        connector._token_expiry = config.token_expiry

    if sync_req.direction == "push":
        # Push invoice(s) to ERP
        if sync_req.invoice_id:
            # Single invoice push
            inv_result = await db.execute(
                select(Invoice).where(
                    Invoice.id == sync_req.invoice_id,
                    Invoice.tenant_id == current_user.tenant_id,
                )
            )
            invoice = inv_result.scalar_one_or_none()
            if not invoice:
                raise HTTPException(status_code=404, detail="Invoice not found")

            erp_invoice = _invoice_to_erp(invoice)
            sync_result = await connector.push_invoice(erp_invoice)
        elif sync_req.invoice_ids:
            # Batch push
            sync_result = ConnectorSyncResponse(
                success=True, connector_type=config.connector_type, direction="push",
            )
            for inv_id in sync_req.invoice_ids[:100]:
                inv_result = await db.execute(
                    select(Invoice).where(
                        Invoice.id == inv_id,
                        Invoice.tenant_id == current_user.tenant_id,
                    )
                )
                invoice = inv_result.scalar_one_or_none()
                if invoice:
                    erp_invoice = _invoice_to_erp(invoice)
                    single_result = await connector.push_invoice(erp_invoice)
                    sync_result.records_synced += single_result.records_synced
                    sync_result.records_failed += single_result.records_failed
                    sync_result.erp_ids.extend(single_result.erp_ids)
                    sync_result.errors.extend(single_result.errors)
        else:
            raise HTTPException(status_code=400, detail="invoice_id or invoice_ids required for push")
    elif sync_req.direction == "pull":
        sync_result_raw = await connector.pull_invoices(
            since=sync_req.since, limit=sync_req.limit
        )
        sync_result = ConnectorSyncResponse(
            success=sync_result_raw.success,
            connector_type=sync_result_raw.connector_type,
            direction=sync_result_raw.direction,
            records_synced=sync_result_raw.records_synced,
            records_failed=sync_result_raw.records_failed,
            erp_ids=sync_result_raw.erp_ids,
            errors=sync_result_raw.errors,
            warnings=sync_result_raw.warnings,
            raw_response=sync_result_raw.raw_response,
        )
    else:
        raise HTTPException(status_code=400, detail="direction must be 'push' or 'pull'")

    # Update connector sync stats
    config.last_sync_at = datetime.utcnow()
    config.total_synced += sync_result.records_synced
    config.total_failed += sync_result.records_failed
    config.last_sync_result = {
        "success": sync_result.success,
        "synced": sync_result.records_synced,
        "failed": sync_result.records_failed,
    }
    config.status = ConnectorStatus.ACTIVE if sync_result.success else ConnectorStatus.ERROR
    await db.commit()

    return sync_result


@router.post("/{connector_id}/activate")
async def activate_connector(
    connector_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Activate an ERP connector."""
    result = await db.execute(
        select(ERPConnectorConfig).where(
            ERPConnectorConfig.id == connector_id,
            ERPConnectorConfig.tenant_id == current_user.tenant_id,
        )
    )
    config = result.scalar_one_or_none()
    if not config:
        raise HTTPException(status_code=404, detail="Connector not found")

    config.status = ConnectorStatus.ACTIVE
    await db.commit()
    return {"status": "active"}


@router.post("/{connector_id}/deactivate")
async def deactivate_connector(
    connector_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Deactivate an ERP connector."""
    result = await db.execute(
        select(ERPConnectorConfig).where(
            ERPConnectorConfig.id == connector_id,
            ERPConnectorConfig.tenant_id == current_user.tenant_id,
        )
    )
    config = result.scalar_one_or_none()
    if not config:
        raise HTTPException(status_code=404, detail="Connector not found")

    config.status = ConnectorStatus.INACTIVE
    await db.commit()
    return {"status": "inactive"}


def _invoice_to_erp(invoice: Invoice) -> ERPInvoice:
    """Convert an Invoice model to ERPInvoice dataclass."""
    return ERPInvoice(
        invoice_number=invoice.invoice_number,
        vendor_name=invoice.vendor_name,
        vendor_tax_id=invoice.vendor_tax_id,
        buyer_name=invoice.buyer_name,
        buyer_tax_id=invoice.buyer_tax_id,
        invoice_date=invoice.invoice_date,
        due_date=invoice.due_date,
        currency=invoice.currency,
        subtotal=invoice.subtotal,
        tax_amount=invoice.tax_amount,
        total_amount=invoice.total_amount,
        country_code=invoice.country_code,
        lines=[
            {
                "description": line.description,
                "quantity": line.quantity,
                "unit_price": line.unit_price,
                "tax_rate": line.tax_rate,
                "tax_amount": line.tax_amount,
                "line_total": line.line_total,
                "item_code": line.item_code,
            }
            for line in invoice.lines
        ] if invoice.lines else [],
        status=invoice.status.value if invoice.status else "draft",
    )
