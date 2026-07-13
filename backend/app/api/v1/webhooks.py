"""Webhook management API endpoints."""

import time
from datetime import datetime
from typing import List
from uuid import UUID

import httpx
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.deps import get_current_user
from app.database import get_db
from app.models.webhook import WebhookConfig
from app.models.user import User
from app.schemas.webhook import (
    WebhookCreate,
    WebhookUpdate,
    WebhookResponse,
    WebhookTestResponse,
)

router = APIRouter()

# All supported webhook event types
SUPPORTED_EVENTS = [
    "invoice.received",
    "invoice.extracted",
    "invoice.approved",
    "invoice.rejected",
    "invoice.compliant",
    "invoice.transmitted",
    "invoice.failed",
    "compliance.processed",
    "erp.synced",
]


@router.get("/events", response_model=List[str])
async def list_supported_events(current_user: User = Depends(get_current_user)):
    """List all supported webhook event types."""
    return SUPPORTED_EVENTS


@router.get("/", response_model=List[WebhookResponse])
async def list_webhooks(
    db: AsyncSession = Depends(get_db),
    current_user: User = Depends(get_current_user),
):
    """List all webhook configurations for the tenant."""
    result = await db.execute(
        select(WebhookConfig)
        .where(WebhookConfig.tenant_id == current_user.tenant_id)
        .order_by(WebhookConfig.created_at.desc())
    )
    return result.scalars().all()


@router.post("/", response_model=WebhookResponse, status_code=201)
async def create_webhook(
    data: WebhookCreate,
    db: AsyncSession = Depends(get_db),
    current_user: User = Depends(get_current_user),
):
    """Create a new webhook configuration."""
    # Validate events
    invalid_events = [e for e in data.events if e not in SUPPORTED_EVENTS]
    if invalid_events:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid events: {invalid_events}. Supported: {SUPPORTED_EVENTS}",
        )

    webhook = WebhookConfig(
        tenant_id=current_user.tenant_id,
        name=data.name,
        url=data.url,
        secret=data.secret,
        events=data.events,
        content_type=data.content_type,
        timeout_seconds=data.timeout_seconds,
        max_retries=data.max_retries,
        active=data.active,
    )
    db.add(webhook)
    await db.commit()
    await db.refresh(webhook)
    return webhook


@router.get("/{webhook_id}", response_model=WebhookResponse)
async def get_webhook(
    webhook_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: User = Depends(get_current_user),
):
    """Get a specific webhook configuration."""
    result = await db.execute(
        select(WebhookConfig).where(
            WebhookConfig.id == webhook_id,
            WebhookConfig.tenant_id == current_user.tenant_id,
        )
    )
    webhook = result.scalar_one_or_none()
    if not webhook:
        raise HTTPException(status_code=404, detail="Webhook not found")
    return webhook


@router.put("/{webhook_id}", response_model=WebhookResponse)
async def update_webhook(
    webhook_id: UUID,
    data: WebhookUpdate,
    db: AsyncSession = Depends(get_db),
    current_user: User = Depends(get_current_user),
):
    """Update a webhook configuration."""
    result = await db.execute(
        select(WebhookConfig).where(
            WebhookConfig.id == webhook_id,
            WebhookConfig.tenant_id == current_user.tenant_id,
        )
    )
    webhook = result.scalar_one_or_none()
    if not webhook:
        raise HTTPException(status_code=404, detail="Webhook not found")

    update_data = data.model_dump(exclude_unset=True)
    if "events" in update_data:
        invalid_events = [e for e in update_data["events"] if e not in SUPPORTED_EVENTS]
        if invalid_events:
            raise HTTPException(status_code=400, detail=f"Invalid events: {invalid_events}")

    for field, value in update_data.items():
        setattr(webhook, field, value)

    await db.commit()
    await db.refresh(webhook)
    return webhook


@router.delete("/{webhook_id}", status_code=204)
async def delete_webhook(
    webhook_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: User = Depends(get_current_user),
):
    """Delete a webhook configuration."""
    result = await db.execute(
        select(WebhookConfig).where(
            WebhookConfig.id == webhook_id,
            WebhookConfig.tenant_id == current_user.tenant_id,
        )
    )
    webhook = result.scalar_one_or_none()
    if not webhook:
        raise HTTPException(status_code=404, detail="Webhook not found")

    await db.delete(webhook)
    await db.commit()


@router.post("/{webhook_id}/test", response_model=WebhookTestResponse)
async def test_webhook(
    webhook_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: User = Depends(get_current_user),
):
    """Send a test event to the webhook."""
    result = await db.execute(
        select(WebhookConfig).where(
            WebhookConfig.id == webhook_id,
            WebhookConfig.tenant_id == current_user.tenant_id,
        )
    )
    webhook = result.scalar_one_or_none()
    if not webhook:
        raise HTTPException(status_code=404, detail="Webhook not found")

    # Build test payload
    import json
    from uuid import uuid4
    test_payload = json.dumps({
        "event_id": str(uuid4()),
        "event_type": "invoice.received",
        "tenant_id": str(current_user.tenant_id),
        "invoice_id": "test-invoice-id",
        "source": "invoiceflow",
        "timestamp": datetime.utcnow().isoformat(),
        "data": {
            "test": True,
            "message": "This is a test webhook delivery from InvoiceFlow",
            "invoice_number": "TEST-001",
        },
    })

    headers = {"Content-Type": webhook.content_type}
    start = time.time()

    try:
        async with httpx.AsyncClient() as client:
            resp = await client.post(
                webhook.url,
                content=test_payload,
                headers=headers,
                timeout=webhook.timeout_seconds,
            )
        duration = (time.time() - start) * 1000

        return WebhookTestResponse(
            success=resp.status_code < 400,
            status_code=resp.status_code,
            message=f"HTTP {resp.status_code}: {resp.text[:200]}",
            response_time_ms=round(duration, 2),
        )
    except httpx.TimeoutException:
        duration = (time.time() - start) * 1000
        return WebhookTestResponse(
            success=False,
            message="Request timed out",
            response_time_ms=round(duration, 2),
        )
    except Exception as e:
        return WebhookTestResponse(
            success=False,
            message=f"Connection error: {str(e)}",
        )


@router.post("/{webhook_id}/toggle")
async def toggle_webhook(
    webhook_id: UUID,
    db: AsyncSession = Depends(get_db),
    current_user: User = Depends(get_current_user),
):
    """Toggle a webhook on/off."""
    result = await db.execute(
        select(WebhookConfig).where(
            WebhookConfig.id == webhook_id,
            WebhookConfig.tenant_id == current_user.tenant_id,
        )
    )
    webhook = result.scalar_one_or_none()
    if not webhook:
        raise HTTPException(status_code=404, detail="Webhook not found")

    webhook.active = not webhook.active
    await db.commit()
    return {"active": webhook.active}
