"""Celery tasks for ERP connector sync."""

import asyncio
import logging
from datetime import datetime
from typing import List, Optional

from app.tasks.celery_app import celery_app
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.config import settings
from app.database import async_session
from app.models.connector import ERPConnectorConfig, ConnectorStatus
from app.models.invoice import Invoice, InvoiceStatus
from app.connectors import get_connector, ERPInvoice

logger = logging.getLogger(__name__)


@celery_app.task(bind=True, max_retries=3, default_retry_delay=60)
def sync_invoice_to_erp(self, invoice_id: str, tenant_id: str, connector_type: str):
    """Push a single invoice to the ERP system."""
    asyncio.run(_sync_invoice_to_erp_async(self, invoice_id, tenant_id, connector_type))


async def _sync_invoice_to_erp_async(
    task, invoice_id: str, tenant_id: str, connector_type: str
):
    """Async implementation of single invoice ERP sync."""
    async with async_session() as db:
        # Fetch connector config
        result = await db.execute(
            select(ERPConnectorConfig).where(
                ERPConnectorConfig.tenant_id == tenant_id,
                ERPConnectorConfig.connector_type == connector_type,
                ERPConnectorConfig.status == ConnectorStatus.ACTIVE,
            )
        )
        config = result.scalar_one_or_none()
        if not config:
            logger.warning(f"No active {connector_type} connector for tenant {tenant_id}")
            return {"success": False, "error": "No active connector"}

        # Fetch invoice
        inv_result = await db.execute(
            select(Invoice).where(
                Invoice.id == invoice_id,
                Invoice.tenant_id == tenant_id,
            )
        )
        invoice = inv_result.scalar_one_or_none()
        if not invoice:
            logger.warning(f"Invoice {invoice_id} not found")
            return {"success": False, "error": "Invoice not found"}

        # Create connector instance
        connector = get_connector(
            connector_type=config.connector_type,
            api_key=config.api_key or "",
            api_secret=config.api_secret or "",
            base_url=config.base_url or "",
            sandbox=config.sandbox,
            tenant_id=tenant_id,
            extra_config=config.extra_config,
        )

        # Set Xero tokens if available
        if config.connector_type == "xero" and config.access_token:
            connector._access_token = config.access_token
            connector._refresh_token = config.refresh_token
            connector._token_expiry = config.token_expiry

        # Build ERP invoice
        erp_invoice = ERPInvoice(
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
                    "line_total": line.line_total,
                }
                for line in invoice.lines
            ],
        )

        # Push to ERP
        try:
            sync_result = await connector.push_invoice(erp_invoice)

            # Update connector stats
            config.last_sync_at = datetime.utcnow()
            config.total_synced += sync_result.records_synced
            config.total_failed += sync_result.records_failed
            config.last_sync_result = {
                "success": sync_result.success,
                "invoice_id": invoice_id,
                "erp_ids": sync_result.erp_ids,
            }
            if sync_result.success:
                config.status = ConnectorStatus.ACTIVE
            else:
                config.status = ConnectorStatus.ERROR
            await db.commit()

            logger.info(f"Invoice {invoice_id} synced to {connector_type}: {sync_result.erp_ids}")
            return {
                "success": sync_result.success,
                "erp_ids": sync_result.erp_ids,
                "errors": sync_result.errors,
            }
        except Exception as e:
            logger.error(f"ERP sync failed for invoice {invoice_id}: {e}")
            config.status = ConnectorStatus.ERROR
            config.last_sync_result = {"success": False, "error": str(e)}
            await db.commit()
            raise task.retry(exc=e)


@celery_app.task(bind=True, max_retries=2, default_retry_delay=120)
def batch_sync_to_erp(self, tenant_id: str, connector_type: str, since_hours: int = 24):
    """Batch sync: push all compliant invoices to ERP."""
    asyncio.run(_batch_sync_async(self, tenant_id, connector_type, since_hours))


async def _batch_sync_async(task, tenant_id: str, connector_type: str, since_hours: int):
    """Async implementation of batch ERP sync."""
    from datetime import timedelta

    async with async_session() as db:
        # Fetch connector config
        result = await db.execute(
            select(ERPConnectorConfig).where(
                ERPConnectorConfig.tenant_id == tenant_id,
                ERPConnectorConfig.connector_type == connector_type,
                ERPConnectorConfig.status == ConnectorStatus.ACTIVE,
            )
        )
        config = result.scalar_one_or_none()
        if not config:
            return {"success": False, "error": "No active connector"}

        # Fetch compliant invoices since cutoff
        cutoff = datetime.utcnow() - timedelta(hours=since_hours)
        inv_result = await db.execute(
            select(Invoice).where(
                Invoice.tenant_id == tenant_id,
                Invoice.status == InvoiceStatus.COMPLIANT,
                Invoice.updated_at >= cutoff,
            )
        )
        invoices = inv_result.scalars().all()

        if not invoices:
            return {"success": True, "synced": 0, "message": "No invoices to sync"}

        # Create connector
        connector = get_connector(
            connector_type=config.connector_type,
            api_key=config.api_key or "",
            api_secret=config.api_secret or "",
            base_url=config.base_url or "",
            sandbox=config.sandbox,
            tenant_id=tenant_id,
            extra_config=config.extra_config,
        )

        # Push each invoice
        total_synced = 0
        total_failed = 0
        all_erp_ids = []
        all_errors = []

        for invoice in invoices:
            erp_invoice = ERPInvoice(
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
                        "line_total": line.line_total,
                    }
                    for line in invoice.lines
                ],
            )

            try:
                sync_result = await connector.push_invoice(erp_invoice)
                total_synced += sync_result.records_synced
                total_failed += sync_result.records_failed
                all_erp_ids.extend(sync_result.erp_ids)
                all_errors.extend(sync_result.errors)

                # Update invoice with ERP reference
                if sync_result.success and sync_result.erp_ids:
                    invoice.compliance_response = {
                        **(invoice.compliance_response or {}),
                        "erp_id": sync_result.erp_ids[0],
                        "erp_connector": connector_type,
                        "erp_synced_at": datetime.utcnow().isoformat(),
                    }
            except Exception as e:
                logger.error(f"Failed to sync invoice {invoice.invoice_number}: {e}")
                total_failed += 1
                all_errors.append(str(e))

        # Update connector stats
        config.last_sync_at = datetime.utcnow()
        config.total_synced += total_synced
        config.total_failed += total_failed
        config.last_sync_result = {
            "success": total_failed == 0,
            "synced": total_synced,
            "failed": total_failed,
        }
        await db.commit()

        return {
            "success": total_failed == 0,
            "synced": total_synced,
            "failed": total_failed,
            "erp_ids": all_erp_ids,
            "errors": all_errors,
        }
