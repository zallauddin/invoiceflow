"""Invoice Pydantic schemas."""

from datetime import datetime
from typing import Optional
from uuid import UUID

from pydantic import BaseModel, Field

from app.models.invoice import InvoiceStatus, ComplianceModel, ExtractionMethod, IngestionSource


class InvoiceLineCreate(BaseModel):
    line_number: int
    description: str | None = None
    quantity: float = 1.0
    unit_price: float = 0.0
    tax_rate: float = 0.0
    item_code: str | None = None


class InvoiceLineResponse(BaseModel):
    id: UUID
    line_number: int
    description: str | None = None
    quantity: float
    unit_price: float
    tax_rate: float
    tax_amount: float
    line_total: float
    item_code: str | None = None

    class Config:
        from_attributes = True


class InvoiceCreate(BaseModel):
    invoice_number: str = Field(..., max_length=100)
    vendor_name: str = Field(..., max_length=255)
    vendor_tax_id: str | None = None
    buyer_name: str | None = None
    buyer_tax_id: str | None = None
    invoice_date: datetime | None = None
    due_date: datetime | None = None
    currency: str = "USD"
    country_code: str = Field(..., max_length=2)
    compliance_model: ComplianceModel
    lines: list[InvoiceLineCreate] = []


class InvoiceUpdate(BaseModel):
    invoice_number: str | None = None
    vendor_name: str | None = None
    vendor_tax_id: str | None = None
    buyer_name: str | None = None
    buyer_tax_id: str | None = None
    invoice_date: datetime | None = None
    due_date: datetime | None = None
    currency: str | None = None
    status: InvoiceStatus | None = None
    lines: list[InvoiceLineCreate] | None = None


class InvoiceResponse(BaseModel):
    id: UUID
    tenant_id: UUID
    status: InvoiceStatus
    invoice_number: str
    vendor_name: str
    vendor_tax_id: str | None = None
    buyer_name: str | None = None
    buyer_tax_id: str | None = None
    invoice_date: datetime | None = None
    due_date: datetime | None = None
    currency: str
    subtotal: float
    tax_amount: float
    total_amount: float
    country_code: str
    compliance_model: ComplianceModel
    source: IngestionSource
    source_reference: str | None = None
    file_url: str | None = None
    original_filename: str | None = None
    ocr_confidence: float | None = None
    extraction_method: ExtractionMethod | None = None
    extracted_data: dict = {}
    compliance_response: dict = {}
    error_message: str | None = None
    created_at: datetime
    updated_at: datetime
    lines: list[InvoiceLineResponse] = []

    class Config:
        from_attributes = True


class InvoiceListResponse(BaseModel):
    invoices: list[InvoiceResponse]
    total: int
    page: int
    page_size: int
