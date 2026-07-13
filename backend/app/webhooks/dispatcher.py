"""Event dispatcher for invoice lifecycle events.

Publishes events to configured webhook endpoints and provides
an in-process event bus for internal subscribers.
"""

import hashlib
import hmac
import json
import logging
import time
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Callable, Dict, List, Optional
from uuid import uuid4

import httpx

logger = logging.getLogger(__name__)


@dataclass
class Event:
    """An invoice lifecycle event."""
    event_type: str
    tenant_id: str
    invoice_id: str
    data: Dict[str, Any]
    event_id: str = field(default_factory=lambda: str(uuid4()))
    timestamp: datetime = field(default_factory=datetime.utcnow)
    source: str = "invoiceflow"


class EventDispatcher:
    """Dispatches events to webhooks and in-process subscribers."""

    def __init__(self):
        self._subscribers: Dict[str, List[Callable]] = {}

    def subscribe(self, event_type: str, handler: Callable) -> None:
        """Subscribe to an event type for in-process handling."""
        if event_type not in self._subscribers:
            self._subscribers[event_type] = []
        self._subscribers[event_type].append(handler)

    def unsubscribe(self, event_type: str, handler: Callable) -> None:
        """Remove a subscriber."""
        if event_type in self._subscribers:
            self._subscribers[event_type] = [
                h for h in self._subscribers[event_type] if h != handler
            ]

    async def dispatch(self, event: Event) -> Dict[str, Any]:
        """Dispatch an event to all subscribers and webhooks."""
        results = {"subscribers": 0, "webhooks": 0, "errors": []}

        # In-process subscribers
        for handler in self._subscribers.get(event.event_type, []):
            try:
                if callable(handler):
                    import asyncio
                    if asyncio.iscoroutinefunction(handler):
                        await handler(event)
                    else:
                        handler(event)
                    results["subscribers"] += 1
            except Exception as e:
                logger.error(f"Subscriber error for {event.event_type}: {e}")
                results["errors"].append(str(e))

        # Wildcard subscribers
        for handler in self._subscribers.get("*", []):
            try:
                import asyncio
                if asyncio.iscoroutinefunction(handler):
                    await handler(event)
                else:
                    handler(event)
                results["subscribers"] += 1
            except Exception as e:
                logger.error(f"Wildcard subscriber error: {e}")

        return results

    async def dispatch_webhooks(
        self,
        event: Event,
        webhook_configs: List[Dict[str, Any]],
    ) -> Dict[str, Any]:
        """Dispatch an event to configured webhook endpoints."""
        results = {"dispatched": 0, "failed": 0, "errors": []}
        payload = self._build_payload(event)

        for config in webhook_configs:
            if not config.get("active", True):
                continue
            if event.event_type not in config.get("events", []):
                continue

            url = config.get("url", "")
            if not url:
                continue

            try:
                headers = {"Content-Type": "application/json"}

                # Add HMAC signature if secret is configured
                secret = config.get("secret")
                if secret:
                    signature = self._sign_payload(payload, secret)
                    headers["X-Webhook-Signature"] = f"sha256={signature}"
                    headers["X-Webhook-Timestamp"] = str(int(time.time()))

                headers["X-Event-ID"] = event.event_id
                headers["X-Event-Type"] = event.event_type
                headers["X-Tenant-ID"] = event.tenant_id

                timeout = config.get("timeout_seconds", 30)

                async with httpx.AsyncClient() as client:
                    resp = await client.post(
                        url,
                        content=payload,
                        headers=headers,
                        timeout=timeout,
                    )

                if resp.status_code < 400:
                    results["dispatched"] += 1
                    logger.info(f"Webhook delivered: {event.event_type} → {url} ({resp.status_code})")
                else:
                    results["failed"] += 1
                    results["errors"].append(f"HTTP {resp.status_code}: {url}")
                    logger.warning(f"Webhook failed: {url} → {resp.status_code}")

            except httpx.TimeoutException:
                results["failed"] += 1
                results["errors"].append(f"Timeout: {url}")
                logger.warning(f"Webhook timeout: {url}")
            except Exception as e:
                results["failed"] += 1
                results["errors"].append(f"{type(e).__name__}: {url}")
                logger.error(f"Webhook error: {url} → {e}")

        return results

    def _build_payload(self, event: Event) -> str:
        """Build the webhook JSON payload."""
        payload = {
            "event_id": event.event_id,
            "event_type": event.event_type,
            "tenant_id": event.tenant_id,
            "invoice_id": event.invoice_id,
            "source": event.source,
            "timestamp": event.timestamp.isoformat(),
            "data": event.data,
        }
        return json.dumps(payload, default=str)

    @staticmethod
    def _sign_payload(payload: str, secret: str) -> str:
        """Compute HMAC-SHA256 signature for the payload."""
        return hmac.new(
            secret.encode("utf-8"),
            payload.encode("utf-8"),
            hashlib.sha256,
        ).hexdigest()


# Global singleton
dispatcher = EventDispatcher()


# ── Event Factory Helpers ────────────────────────────────────────────

def create_invoice_event(
    event_type: str,
    invoice_data: Dict[str, Any],
    tenant_id: str,
) -> Event:
    """Create an Event from invoice data."""
    return Event(
        event_type=event_type,
        tenant_id=tenant_id,
        invoice_id=invoice_data.get("id", ""),
        data=invoice_data,
    )


# ── Pre-built event types ────────────────────────────────────────────

class InvoiceEvents:
    """Convenience class for common invoice events."""
    RECEIVED = "invoice.received"
    EXTRACTED = "invoice.extracted"
    APPROVED = "invoice.approved"
    REJECTED = "invoice.rejected"
    COMPLIANT = "invoice.compliant"
    TRANSMITTED = "invoice.transmitted"
    FAILED = "invoice.failed"
    COMPLIANCE_PROCESSED = "compliance.processed"
    ERP_SYNCED = "erp.synced"
