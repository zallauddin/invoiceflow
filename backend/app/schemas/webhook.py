"""Webhook Pydantic schemas."""

from datetime import datetime
from typing import Any, Dict, List, Optional
from pydantic import BaseModel, HttpUrl


class WebhookCreate(BaseModel):
    name: str
    url: str
    secret: Optional[str] = None
    events: List[str] = ["invoice.received", "invoice.extracted"]
    content_type: str = "application/json"
    timeout_seconds: int = 30
    max_retries: int = 3
    active: bool = True


class WebhookUpdate(BaseModel):
    name: Optional[str] = None
    url: Optional[str] = None
    secret: Optional[str] = None
    events: Optional[List[str]] = None
    active: Optional[bool] = None
    timeout_seconds: Optional[int] = None
    max_retries: Optional[int] = None


class WebhookResponse(BaseModel):
    id: str
    name: str
    url: str
    active: bool
    events: List[str]
    content_type: str
    timeout_seconds: int
    max_retries: int
    last_triggered_at: Optional[datetime] = None
    last_status_code: Optional[int] = None
    success_count: int = 0
    failure_count: int = 0
    created_at: datetime

    class Config:
        from_attributes = True


class WebhookTestResponse(BaseModel):
    success: bool
    status_code: Optional[int] = None
    message: str
    response_time_ms: Optional[float] = None


class EventPayload(BaseModel):
    event_id: str
    event_type: str
    tenant_id: str
    invoice_id: str
    source: str = "invoiceflow"
    timestamp: datetime
    data: Dict[str, Any]


class WebhookDeliveryLog(BaseModel):
    id: str
    webhook_id: str
    event_type: str
    status_code: Optional[int] = None
    success: bool
    error: Optional[str] = None
    duration_ms: Optional[float] = None
    timestamp: datetime
