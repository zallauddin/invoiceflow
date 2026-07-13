"""Compliance configuration model."""

import uuid
from datetime import datetime

from sqlalchemy import Column, String, DateTime, Boolean, JSON, ForeignKey
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship

from app.database import Base


class ComplianceConfig(Base):
    __tablename__ = "compliance_configs"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    tenant_id = Column(UUID(as_uuid=True), ForeignKey("tenants.id"), nullable=False, index=True)

    country_code = Column(String(2), nullable=False)
    model = Column(String(20), nullable=False)  # peppol, clearance, ctc, post_audit
    enabled = Column(Boolean, default=True, nullable=False)

    # Country-specific config (API keys, endpoint URLs, certificates, etc.)
    config = Column(JSON, default=dict)

    # Timestamps
    created_at = Column(DateTime, default=datetime.utcnow, nullable=False)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, nullable=False)

    # Relationships
    tenant = relationship("Tenant", back_populates="compliance_configs")
