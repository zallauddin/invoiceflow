"""Tenant SQLAlchemy model for multi-tenancy."""

import uuid
from datetime import datetime

from sqlalchemy import Column, String, DateTime, Boolean, JSON
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship

from app.database import Base


class Tenant(Base):
    __tablename__ = "tenants"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    name = Column(String(255), nullable=False)
    slug = Column(String(100), unique=True, nullable=False, index=True)

    # Business info
    country_code = Column(String(2), nullable=False)
    tax_id = Column(String(50), nullable=True)
    registration_number = Column(String(100), nullable=True)

    # Settings
    default_currency = Column(String(3), default="USD")
    default_compliance_model = Column(String(20), default="peppol")
    settings = Column(JSON, default=dict)

    # Status
    is_active = Column(Boolean, default=True, nullable=False)

    # Timestamps
    created_at = Column(DateTime, default=datetime.utcnow, nullable=False)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, nullable=False)

    # Relationships
    invoices = relationship("Invoice", back_populates="tenant")
    compliance_configs = relationship("ComplianceConfig", back_populates="tenant", cascade="all, delete-orphan")
    users = relationship("User", back_populates="tenant", cascade="all, delete-orphan")
