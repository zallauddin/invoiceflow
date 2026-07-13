"""Webhook configuration SQLAlchemy model."""

import enum
import uuid
from datetime import datetime

from sqlalchemy import (
    Column, String, Integer, Enum, DateTime, ForeignKey, Text, JSON, Boolean, Index,
)
from sqlalchemy.dialects.postgresql import UUID

from app.database import Base


class WebhookEventType(str, enum.Enum):
    INVOICE_RECEIVED = "invoice.received"
    INVOICE_EXTRACTED = "invoice.extracted"
    INVOICE_APPROVED = "invoice.approved"
    INVOICE_REJECTED = "invoice.rejected"
    INVOICE_COMPLIANT = "invoice.compliant"
    INVOICE_TRANSMITTED = "invoice.transmitted"
    INVOICE_FAILED = "invoice.failed"
    COMPLIANCE_PROCESSED = "compliance.processed"
    ERP_SYNCED = "erp.synced"


class WebhookConfig(Base):
    """Webhook endpoint configurations per tenant."""
    __tablename__ = "webhook_configs"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    tenant_id = Column(UUID(as_uuid=True), ForeignKey("tenants.id"), nullable=False, index=True)

    # Webhook details
    name = Column(String(100), nullable=False)
    url = Column(String(500), nullable=False)
    secret = Column(String(255), nullable=True)  # For HMAC signature verification
    active = Column(Boolean, default=True, nullable=False)

    # Events to subscribe to
    events = Column(JSON, default=list, nullable=False)  # List of WebhookEventType values

    # Config
    content_type = Column(String(50), default="application/json")
    timeout_seconds = Column(Integer, default=30)
    max_retries = Column(Integer, default=3)

    # Stats
    last_triggered_at = Column(DateTime, nullable=True)
    last_status_code = Column(Integer, nullable=True)
    success_count = Column(Integer, default=0)
    failure_count = Column(Integer, default=0)

    # Metadata
    created_at = Column(DateTime, default=datetime.utcnow, nullable=False)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, nullable=False)

    __table_args__ = (
        Index("ix_webhook_configs_tenant", "tenant_id"),
    )
