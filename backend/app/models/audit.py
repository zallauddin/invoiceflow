"""Audit log model for compliance trail."""

import uuid
from datetime import datetime

from sqlalchemy import Column, String, DateTime, JSON, ForeignKey, Text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship

from app.database import Base


class AuditLog(Base):
    __tablename__ = "audit_logs"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    tenant_id = Column(UUID(as_uuid=True), ForeignKey("tenants.id"), nullable=False, index=True)
    invoice_id = Column(UUID(as_uuid=True), ForeignKey("invoices.id"), nullable=True, index=True)

    # Action details
    action = Column(String(100), nullable=False, index=True)
    details = Column(JSON, default=dict)
    message = Column(Text, nullable=True)

    # User tracking
    user_id = Column(String(100), nullable=True)
    ip_address = Column(String(45), nullable=True)

    # Timestamp
    timestamp = Column(DateTime, default=datetime.utcnow, nullable=False, index=True)

    # Relationships
    invoice = relationship("Invoice", back_populates="audit_logs")
