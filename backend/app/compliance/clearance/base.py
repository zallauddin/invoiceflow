"""Base clearance compliance handler.

Clearance model: Invoice is submitted to a government authority for validation
before it can be sent to the buyer. The authority issues a clearance number/hash.
Countries using clearance: Saudi Arabia (ZATCA), Brazil (SEFAZ), India (IRP), Mexico (SAT).
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Any
from datetime import datetime
import uuid


@dataclass
class ClearanceRequest:
    """Data needed to submit for clearance."""
    invoice_number: str
    invoice_date: str
    vendor_name: str
    vendor_tax_id: str
    buyer_name: str
    buyer_tax_id: str
    currency: str
    subtotal: float
    tax_amount: float
    total_amount: float
    country_code: str
    lines: list[dict[str, Any]] = field(default_factory=list)
    extra_fields: dict[str, Any] = field(default_factory=dict)


@dataclass
class ClearanceResult:
    """Result of clearance submission."""
    success: bool = False
    clearance_id: str = ""
    clearance_hash: str = ""
    timestamp: str = ""
    status: str = "pending"
    raw_response: dict = field(default_factory=list)
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)


class BaseClearance(ABC):
    """Abstract base for country-specific clearance implementations."""

    country_code: str = ""
    country_name: str = ""
    clearance_model: str = "clearance"

    @abstractmethod
    async def validate(self, request: ClearanceRequest) -> ClearanceResult:
        """Validate invoice against country-specific rules."""
        ...

    @abstractmethod
    async def submit(self, request: ClearanceRequest) -> ClearanceResult:
        """Submit invoice for clearance."""
        ...

    @abstractmethod
    async def check_status(self, clearance_id: str) -> ClearanceResult:
        """Check the status of a submitted clearance."""
        ...

    def _generate_uuid(self) -> str:
        return str(uuid.uuid4())

    def _now_iso(self) -> str:
        return datetime.utcnow().isoformat() + "Z"
