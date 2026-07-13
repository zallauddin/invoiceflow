"""Celery tasks for webhook delivery."""

import asyncio
import json
import logging
import time
from datetime import datetime

import httpx
from app.tasks.celery_app import celery_app
from sqlalchemy import select

from app.database import async_session
from app.models.webhook import WebhookConfig

logger = logging.getLogger(__name__)


@celery_app.task(bind=True, max_retries=3, default_retry_delay=30)
def deliver_webhook(
    self,
    webhook_id: str,
    event_type: str,
    tenant_id: str,
    invoice_id: str,
    event_data: dict,
):
    """Deliver a webhook event to a specific endpoint."""
    asyncio.run(_deliver_webhook_async(
        self, webhook_id, event_type, tenant_id, invoice_id, event_data
    ))


async def _deliver_webhook_async(
    task,
    webhook_id: str,
    event_type: str,
    tenant_id: str,
    invoice_id: str,
    event_data: dict,
):
    """Async webhook delivery."""
    from uuid import uuid4

    async with async_session() as db:
        result = await db.execute(
            select(WebhookConfig).where(WebhookConfig.id == webhook_id)
        )
        config = result.scalar_one_or_none()
        if not config or not config.active:
            return

        payload = json.dumps({
            "event_id": str(uuid4()),
            "event_type": event_type,
            "tenant_id": tenant_id,
            "invoice_id": invoice_id,
            "source": "invoiceflow",
            "timestamp": datetime.utcnow().isoformat(),
            "data": event_data,
        }, default=str)

        headers = {"Content-Type": config.content_type}
        if config.secret:
            import hmac
            import hashlib
            sig = hmac.new(
                config.secret.encode(), payload.encode(), hashlib.sha256
            ).hexdigest()
            headers["X-Webhook-Signature"] = f"sha256={sig}"

        start = time.time()
        try:
            async with httpx.AsyncClient() as client:
                resp = await client.post(
                    config.url,
                    content=payload,
                    headers=headers,
                    timeout=config.timeout_seconds,
                )
            duration = (time.time() - start) * 1000

            config.last_triggered_at = datetime.utcnow()
            config.last_status_code = resp.status_code
            if resp.status_code < 400:
                config.success_count += 1
            else:
                config.failure_count += 1
            await db.commit()

            if resp.status_code >= 400:
                raise Exception(f"HTTP {resp.status_code}")

            logger.info(f"Webhook delivered: {event_type} → {config.url} ({resp.status_code})")

        except Exception as e:
            config.last_triggered_at = datetime.utcnow()
            config.failure_count += 1
            await db.commit()

            logger.error(f"Webhook delivery failed: {config.url} → {e}")
            raise task.retry(exc=e)


@celery_app.task(bind=True, max_retries=1)
def dispatch_webhooks_for_event(
    self,
    event_type: str,
    tenant_id: str,
    invoice_id: str,
    event_data: dict,
):
    """Find matching webhooks and queue delivery tasks."""
    asyncio.run(_dispatch_webhooks_async(
        self, event_type, tenant_id, invoice_id, event_data
    ))


async def _dispatch_webhooks_async(
    task,
    event_type: str,
    tenant_id: str,
    invoice_id: str,
    event_data: dict,
):
    """Find webhooks subscribed to this event and dispatch."""
    async with async_session() as db:
        result = await db.execute(
            select(WebhookConfig).where(
                WebhookConfig.tenant_id == tenant_id,
                WebhookConfig.active == True,
            )
        )
        webhooks = result.scalars().all()

        dispatched = 0
        for wh in webhooks:
            if event_type in (wh.events or []):
                deliver_webhook.delay(
                    webhook_id=str(wh.id),
                    event_type=event_type,
                    tenant_id=tenant_id,
                    invoice_id=invoice_id,
                    event_data=event_data,
                )
                dispatched += 1

        logger.info(
            f"Dispatched webhook delivery for {event_type}: "
            f"{dispatched} webhook(s) for tenant {tenant_id}"
        )
        return {"dispatched": dispatched}
