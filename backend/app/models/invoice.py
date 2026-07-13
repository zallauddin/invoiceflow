"""Invoice SQLAlchemy models."""

import enum
import uuid
from datetime import datetime

from sqlalchemy import (
    Column, String, Integer, Float, Enum, DateTime, ForeignKey, Text, JSON, Boolean, Index,
)
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship

from app.database import Base


class InvoiceStatus(str, enum.Enum):
    RECEIVED = "received"
    PROCESSING = "processing"
    EXTRACTED = "extracted"
    REVIEWING = "reviewing"
    APPROVED = "approved"
    COMPLIANT = "compliant"
    TRANSMITTED = "transmitted"
    FAILED = "failed"
    REJECTED = "rejected"


class ComplianceModel(str, enum.Enum):
    PEPPOL = "peppol"
    CLEARANCE = "clearance"
    CTC = "ctc"
    POST_AUDIT = "post_audit"


class ExtractionMethod(str, enum.Enum):
    OCR = "ocr"
    LLM = "llm"
    XML = "xml"
    MANUAL = "manual"


class IngestionSource(str, enum.Enum):
    EMAIL = "email"
    FTP = "ftp"
    SFTP = "sftp"
    API = "api"
    WEBHOOK = "webhook"
    MANUAL = "manual"


class Invoice(Base):
    __tablename__ = "invoices"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    tenant_id = Column(UUID(as_uuid=True), ForeignKey("tenants.id"), nullable=False, index=True)

    # Status
    status = Column(Enum(InvoiceStatus), default=InvoiceStatus.RECEIVED, nullable=False, index=True)

    # Invoice identification
    invoice_number = Column(String(100), nullable=False)
    vendor_name = Column(String(255), nullable=False)
    vendor_tax_id = Column(String(50), nullable=True)
    buyer_name = Column(String(255), nullable=True)
    buyer_tax_id = Column(String(50), nullable=True)

    # Dates
    invoice_date = Column(DateTime, nullable=True)
    due_date = Column(DateTime, nullable=True)

    # Financials
    currency = Column(String(3), default="USD", nullable=False)
    subtotal = Column(Float, default=0.0, nullable=False)
    tax_amount = Column(Float, default=0.0, nullable=False)
    total_amount = Column(Float, default=0.0, nullable=False)

    # Compliance
    country_code = Column(String(2), nullable=False, index=True)
    compliance_model = Column(Enum(ComplianceModel), nullable=False)
    compliance_response = Column(JSON, default=dict)

    # Source tracking
    source = Column(Enum(IngestionSource), nullable=False)
    source_reference = Column(String(255), nullable=True)

    # Document storage
    file_url = Column(String(500), nullable=True)
    original_filename = Column(String(255), nullable=True)
    mime_type = Column(String(100), nullable=True)

    # AI extraction
    ocr_confidence = Column(Float, nullable=True)
    extraction_method = Column(Enum(ExtractionMethod), nullable=True)
    extracted_data = Column(JSON, default=dict)

    # Processing
    processing_started_at = Column(DateTime, nullable=True)
    processing_completed_at = Column(DateTime, nullable=True)
    error_message = Column(Text, nullable=True)

    # Metadata
    created_at = Column(DateTime, default=datetime.utcnow, nullable=False)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, nullable=False)

    # Relationships
    lines = relationship("InvoiceLine", back_populates="invoice", cascade="all, delete-orphan")
    tenant = relationship("Tenant", back_populates="invoices")
    audit_logs = relationship("AuditLog", back_populates="invoice", cascade="all, delete-orphan")

    __table_args__ = (
        Index("ix_invoices_tenant_status", "tenant_id", "status"),
        Index("ix_invoices_tenant_country", "tenant_id", "country_code"),
        Index("ix_invoices_created", "created_at"),
    )


class InvoiceLine(Base):
    __tablename__ = "invoice_lines"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    invoice_id = Column(UUID(as_uuid=True), ForeignKey("invoices.id", ondelete="CASCADE"), nullable=False, index=True)

    line_number = Column(Integer, nullable=False)
    description = Column(Text, nullable=True)
    quantity = Column(Float, default=1.0, nullable=False)
    unit_price = Column(Float, default=0.0, nullable=False)
    tax_rate = Column(Float, default=0.0, nullable=False)
    tax_amount = Column(Float, default=0.0, nullable=False)
    line_total = Column(Float, default=0.0, nullable=False)
    item_code = Column(String(50), nullable=True)

    # Relationship
    invoice = relationship("Invoice", back_populates="lines")

    __table_args__ = (
        Index("ix_invoice_lines_invoice_number", "invoice_id", "line_number"),
    )
