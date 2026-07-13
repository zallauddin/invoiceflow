"""ERP Connector SQLAlchemy model."""

import enum
import uuid
from datetime import datetime

from sqlalchemy import (
    Column, String, Integer, Enum, DateTime, ForeignKey, Text, JSON, Boolean, Index,
)
from sqlalchemy.dialects.postgresql import UUID

from app.database import Base


class ConnectorStatus(str, enum.Enum):
    ACTIVE = "active"
    INACTIVE = "inactive"
    ERROR = "error"
    PENDING_AUTH = "pending_auth"


class ERPConnectorConfig(Base):
    """Stored ERP connector configurations per tenant."""
    __tablename__ = "erp_connector_configs"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    tenant_id = Column(UUID(as_uuid=True), ForeignKey("tenants.id"), nullable=False, index=True)

    # Connector details
    connector_type = Column(String(50), nullable=False)  # sap, oracle, xero
    display_name = Column(String(100), nullable=False)
    status = Column(Enum(ConnectorStatus), default=ConnectorStatus.INACTIVE, nullable=False)

    # Credentials (encrypted in production)
    api_key = Column(Text, nullable=True)
    api_secret = Column(Text, nullable=True)
    base_url = Column(String(500), nullable=True)

    # Configuration
    sandbox = Column(Boolean, default=True, nullable=False)
    sync_direction = Column(String(20), default="push", nullable=False)  # push, pull, bidirectional
    extra_config = Column(JSON, default=dict)

    # OAuth tokens (for Xero, etc.)
    access_token = Column(Text, nullable=True)
    refresh_token = Column(Text, nullable=True)
    token_expiry = Column(DateTime, nullable=True)

    # Sync state
    last_sync_at = Column(DateTime, nullable=True)
    last_sync_result = Column(JSON, default=dict)
    total_synced = Column(Integer, default=0)
    total_failed = Column(Integer, default=0)

    # Metadata
    created_at = Column(DateTime, default=datetime.utcnow, nullable=False)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, nullable=False)

    __table_args__ = (
        Index("ix_erp_configs_tenant_type", "tenant_id", "connector_type", unique=True),
    )
