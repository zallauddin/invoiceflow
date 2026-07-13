"""Ingestion Pydantic schemas."""

from datetime import datetime
from typing import Optional
from uuid import UUID

from pydantic import BaseModel, Field

from app.models.invoice import IngestionSource, InvoiceStatus


class IngestionSourceCreate(BaseModel):
    """Schema for creating a new ingestion source configuration."""
    source_type: IngestionSource
    name: str = Field(..., max_length=100)
    config: dict = Field(default_factory=dict, description="Source-specific config (host, port, credentials ref, etc.)")
    is_active: bool = True


class IngestionSourceResponse(BaseModel):
    """Response for an ingestion source."""
    id: UUID
    source_type: IngestionSource
    name: str
    is_active: bool
    last_polled_at: Optional[datetime] = None
    config: dict = {}


class FileUploadResponse(BaseModel):
    """Response after uploading a file."""
    invoice_id: Optional[UUID] = None
    status: InvoiceStatus
    filename: str
    message: str


class WebhookPayload(BaseModel):
    """Payload for webhook-based ingestion."""
    filename: str
    file_content_b64: str = Field(..., description="Base64-encoded file content")
    mime_type: str = "application/pdf"
    source_reference: str = ""
    metadata: dict = {}


class PollResponse(BaseModel):
    """Response after triggering a poll."""
    task_id: str
    source_type: IngestionSource
    status: str = "queued"
    message: str = ""


class IngestionStatsResponse(BaseModel):
    """Ingestion statistics."""
    total_ingested: int = 0
    by_source: dict[str, int] = {}
    last_poll_at: Optional[datetime] = None
    pending_processing: int = 0
