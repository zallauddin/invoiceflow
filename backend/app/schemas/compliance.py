"""Compliance Pydantic schemas."""

from uuid import UUID
from datetime import datetime
from typing import Any

from pydantic import BaseModel


class ComplianceConfigCreate(BaseModel):
    country_code: str
    model: str  # peppol, clearance, ctc, post_audit
    enabled: bool = True
    config: dict = {}


class ComplianceConfigResponse(BaseModel):
    id: UUID
    tenant_id: UUID
    country_code: str
    model: str
    enabled: bool
    config: dict
    created_at: datetime
    updated_at: datetime

    class Config:
        from_attributes = True


class ComplianceStatusResponse(BaseModel):
    pending: int
    compliant: int
    failed: int


class ComplianceValidationResponse(BaseModel):
    valid: bool
    errors: list[str] = []
    warnings: list[str] = []


class ComplianceProcessRequest(BaseModel):
    invoice_id: str


class ComplianceBatchRequest(BaseModel):
    invoice_ids: list[str]


class ComplianceResultResponse(BaseModel):
    success: bool
    model: str
    country_code: str
    status: str
    clearance_id: str = ""
    errors: list[str] = []
    warnings: list[str] = []


class ComplianceTaskResponse(BaseModel):
    task_id: str
    status: str
    invoice_id: str = ""
    result: dict[str, Any] | None = None
