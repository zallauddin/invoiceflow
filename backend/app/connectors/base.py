"""Base ERP connector interface."""

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional
from enum import Enum


class ConnectorType(str, Enum):
    SAP = "sap"
    ORACLE = "oracle"
    XERO = "xero"
    QUICKBOOKS = "quickbooks"
    SAGE = "sage"


class SyncDirection(str, Enum):
    PUSH = "push"     # InvoiceFlow → ERP
    PULL = "pull"     # ERP → InvoiceFlow
    BIDIRECTIONAL = "bidirectional"


@dataclass
class ERPInvoice:
    """Normalized invoice representation for ERP sync."""
    invoice_number: str
    vendor_name: str
    vendor_tax_id: Optional[str] = None
    buyer_name: Optional[str] = None
    buyer_tax_id: Optional[str] = None
    invoice_date: Optional[datetime] = None
    due_date: Optional[datetime] = None
    currency: str = "USD"
    subtotal: float = 0.0
    tax_amount: float = 0.0
    total_amount: float = 0.0
    country_code: str = ""
    lines: List[Dict[str, Any]] = field(default_factory=list)
    status: str = "draft"
    erp_id: Optional[str] = None  # ID in the ERP system
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class SyncResult:
    """Result of an ERP sync operation."""
    success: bool
    connector_type: str
    direction: str
    records_synced: int = 0
    records_failed: int = 0
    erp_ids: List[str] = field(default_factory=list)
    errors: List[str] = field(default_factory=list)
    warnings: List[str] = field(default_factory=list)
    timestamp: datetime = field(default_factory=datetime.utcnow)
    raw_response: Optional[Dict[str, Any]] = None


class BaseERPConnector(ABC):
    """Abstract base class for ERP connectors."""

    connector_type: ConnectorType
    display_name: str
    supported_directions: List[SyncDirection]

    def __init__(
        self,
        api_key: str = "",
        api_secret: str = "",
        base_url: str = "",
        sandbox: bool = True,
        tenant_id: str = "",
        extra_config: Optional[Dict[str, str]] = None,
    ):
        self.api_key = api_key
        self.api_secret = api_secret
        self.base_url = base_url
        self.sandbox = sandbox
        self.tenant_id = tenant_id
        self.extra_config = extra_config or {}

    @abstractmethod
    async def authenticate(self) -> bool:
        """Authenticate with the ERP system. Returns True if successful."""
        ...

    @abstractmethod
    async def push_invoice(self, invoice: ERPInvoice) -> SyncResult:
        """Push an invoice from InvoiceFlow to the ERP system."""
        ...

    @abstractmethod
    async def pull_invoices(
        self,
        since: Optional[datetime] = None,
        limit: int = 100,
    ) -> SyncResult:
        """Pull invoices from the ERP system into InvoiceFlow."""
        ...

    @abstractmethod
    async def check_status(self, erp_id: str) -> Dict[str, Any]:
        """Check the status of an invoice in the ERP system."""
        ...

    @abstractmethod
    async def test_connection(self) -> Dict[str, Any]:
        """Test the connection to the ERP system."""
        ...

    def to_erp_invoice(self, invoice_data: Dict[str, Any]) -> ERPInvoice:
        """Convert a generic invoice dict to ERPInvoice."""
        return ERPInvoice(
            invoice_number=invoice_data.get("invoice_number", ""),
            vendor_name=invoice_data.get("vendor_name", ""),
            vendor_tax_id=invoice_data.get("vendor_tax_id"),
            buyer_name=invoice_data.get("buyer_name"),
            buyer_tax_id=invoice_data.get("buyer_tax_id"),
            invoice_date=invoice_data.get("invoice_date"),
            due_date=invoice_data.get("due_date"),
            currency=invoice_data.get("currency", "USD"),
            subtotal=invoice_data.get("subtotal", 0.0),
            tax_amount=invoice_data.get("tax_amount", 0.0),
            total_amount=invoice_data.get("total_amount", 0.0),
            country_code=invoice_data.get("country_code", ""),
            lines=invoice_data.get("lines", []),
            status=invoice_data.get("status", "draft"),
        )
