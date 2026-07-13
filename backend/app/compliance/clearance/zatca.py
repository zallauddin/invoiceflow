"""ZATCA Clearance — Saudi Arabia FATOORAH e-Invoice clearance.

ZATCA (Zakat, Tax and Customs Authority) requires all B2B invoices to be
cleared through their FATOORAH system before transmission to the buyer.

Integration modes:
- Phase 1 (Integration): Generate QR code + XML, store locally
- Phase 2 (Reporting/Clearance): Real-time submission to ZATCA API

This implementation handles Phase 2 clearance with sandbox + production support.
"""

import base64
import hashlib
import json
import logging
from datetime import datetime
from typing import Any

import httpx

from app.compliance.clearance.base import BaseClearance, ClearanceRequest, ClearanceResult

logger = logging.getLogger(__name__)


class ZATCAClearance(BaseClearance):
    """Saudi Arabia ZATCA FATOORAH clearance handler."""

    country_code = "SA"
    country_name = "Saudi Arabia"
    clearance_model = "zatca"

    # ZATCA API endpoints
    SANDBOX_BASE = "https://gw-ap-preprod.zatca.gov.sa"
    PRODUCTION_BASE = "https://gw-ap.zatca.gov.sa"
    ISSUE_INVOICE_PATH = "/e-invoicing/developer-portal/invoices"

    # Invoice type codes (ZATCA specific)
    INVOICE_TYPES = {
        "standard": "388",
        "simplified": "381",
        "credit_note": "381",
        "debit_note": "383",
    }

    # Tax categories
    TAX_CATEGORIES = {
        "standard": {"code": "S", "rate": 15.0},
        "exempt": {"code": "O", "rate": 0},
        "zero_rated": {"code": "Z", "rate": 0},
    }

    def __init__(self, sandbox: bool = True, api_key: str = "", api_secret: str = ""):
        self.sandbox = sandbox
        self.api_key = api_key
        self.api_secret = api_secret
        self.base_url = self.SANDBOX_BASE if sandbox else self.PRODUCTION_BASE
        self._client: httpx.AsyncClient | None = None

    async def _get_client(self) -> httpx.AsyncClient:
        if self._client is None or self._client.is_closed:
            self._client = httpx.AsyncClient(timeout=30.0)
        return self._client

    async def validate(self, request: ClearanceRequest) -> ClearanceResult:
        """Validate invoice against ZATCA rules."""
        errors = []
        warnings = []

        # Validate required fields
        if not request.vendor_tax_id:
            errors.append("Vendor VAT number is required for ZATCA")
        elif not request.vendor_tax_id.startswith("3") and len(request.vendor_tax_id) != 15:
            errors.append("Invalid Saudi VAT number format (must be 15 digits starting with 3)")

        if not request.buyer_tax_id:
            warnings.append("Buyer VAT number missing — will be treated as B2C simplified invoice")

        if request.currency != "SAR":
            warnings.append(f"Currency {request.currency} will be converted to SAR for ZATCA reporting")

        if request.total_amount <= 0:
            errors.append("Total amount must be positive")

        if not request.lines:
            errors.append("At least one invoice line is required")

        # Validate line items
        for i, line in enumerate(request.lines, 1):
            if line.get("tax_rate", 0) not in (0, 5, 15):
                errors.append(f"Line {i}: Invalid ZATCA tax rate {line.get('tax_rate')}% (must be 0, 5, or 15)")
            if line.get("quantity", 0) <= 0:
                errors.append(f"Line {i}: Quantity must be positive")

        return ClearanceResult(
            success=len(errors) == 0,
            errors=errors,
            warnings=warnings,
            timestamp=self._now_iso(),
        )

    async def submit(self, request: ClearanceRequest) -> ClearanceResult:
        """Submit invoice to ZATCA for clearance."""
        # First validate
        validation = await self.validate(request)
        if not validation.success:
            return validation

        # Build ZATCA-specific invoice XML
        zatca_invoice = self._build_zatca_invoice(request)

        # Generate mandatory QR code data (base64 encoded TLV)
        qr_data = self._generate_qr_data(request)

        # Build the submission payload
        payload = {
            "invoice": {
                "issueDate": request.invoice_date or datetime.utcnow().strftime("%Y-%m-%d"),
                "issueTime": datetime.utcnow().strftime("%H:%M:%SZ"),
                "invoiceNumber": request.invoice_number,
                "invoiceTypeCode": self.INVOICE_TYPES.get(
                    request.extra_fields.get("type", "standard"), "388"
                ),
                "invoiceCurrencyCode": request.currency,
                "taxCurrencyCode": "SAR",
                "lineCountNumeric": len(request.lines),
                "billingReference": [],
                "accountingSupplierParty": {
                    "partyIdentification": [
                        {"id": {"value": request.vendor_tax_id, "schemeID": "VAT"}}
                    ],
                    "postalAddress": {},
                    "partyTaxScheme": {
                        "companyID": request.vendor_tax_id,
                        "taxScheme": {"id": "VAT"},
                    },
                },
                "accountingCustomerParty": {
                    "partyIdentification": [
                        {"id": {"value": request.buyer_tax_id or "N/A", "schemeID": "VAT"}}
                    ] if request.buyer_tax_id else [],
                    "postalAddress": {},
                },
                "taxTotal": [
                    {
                        "taxAmount": {"value": request.tax_amount, "currencyID": "SAR"},
                        "taxCurrencyCode": "SAR",
                        "taxSubtotal": [
                            {
                                "taxableAmount": {"value": request.subtotal, "currencyID": "SAR"},
                                "taxAmount": {"value": request.tax_amount, "currencyID": "SAR"},
                                "taxCategory": {
                                    "id": "S",
                                    "percent": 15.0,
                                    "taxScheme": {"id": "VAT"},
                                },
                            }
                        ],
                    }
                ],
                "legalMonetaryTotal": {
                    "lineExtensionAmount": {"value": request.subtotal, "currencyID": "SAR"},
                    "taxExclusiveAmount": {"value": request.subtotal, "currencyID": "SAR"},
                    "taxInclusiveAmount": {"value": request.total_amount, "currencyID": "SAR"},
                    "payableAmount": {"value": request.total_amount, "currencyID": "SAR"},
                },
                "invoiceLine": [],
            },
            "qrCode": qr_data,
            "submissionMode": "clearance",
        }

        # Add invoice lines
        for i, line in enumerate(request.lines, 1):
            qty = line.get("quantity", 1)
            price = line.get("unit_price", 0)
            line_total = price * qty
            tax = line_total * (line.get("tax_rate", 15) / 100)

            payload["invoice"]["invoiceLine"].append({
                "id": str(i),
                "invoicedQuantity": {"value": qty, "unitCode": "EA"},
                "lineExtensionAmount": {"value": line_total, "currencyID": "SAR"},
                "item": {
                    "name": line.get("description", f"Item {i}"),
                    "classifiedTaxCategory": {
                        "id": "S",
                        "percent": line.get("tax_rate", 15),
                        "taxScheme": {"id": "VAT"},
                    },
                },
                "price": {"priceAmount": {"value": price, "currencyID": "SAR"}},
            })

        if self.sandbox:
            return await self._sandbox_submit(payload, request)
        return await self._production_submit(payload, request)

    async def _sandbox_submit(self, payload: dict, request: ClearanceRequest) -> ClearanceResult:
        """Simulate ZATCA clearance in sandbox mode."""
        logger.info(f"[ZATCA SANDBOX] Simulating clearance for {request.invoice_number}")

        import asyncio
        await asyncio.sleep(0.1)

        clearance_id = f"ZATCA-{self._generate_uuid()[:12].upper()}"
        clearance_hash = hashlib.sha256(json.dumps(payload, default=str).encode()).hexdigest()[:32]

        return ClearanceResult(
            success=True,
            clearance_id=clearance_id,
            clearance_hash=clearance_hash,
            timestamp=self._now_iso(),
            status="cleared",
            raw_response={
                "status": "CLEARED",
                "clearanceId": clearance_id,
                "clearanceHash": clearance_hash,
                "invoiceNumber": request.invoice_number,
                "note": "Sandbox mode — no real ZATCA submission",
            },
        )

    async def _production_submit(self, payload: dict, request: ClearanceRequest) -> ClearanceResult:
        """Submit to real ZATCA API."""
        logger.info(f"[ZATCA] Submitting invoice {request.invoice_number} for clearance")

        client = await self._get_client()
        try:
            response = await client.post(
                f"{self.base_url}{self.ISSUE_INVOICE_PATH}",
                json=payload,
                headers={
                    "Authorization": f"Basic {self.api_key}",
                    "Accept": "application/json",
                },
            )

            if response.status_code in (200, 201):
                data = response.json()
                return ClearanceResult(
                    success=True,
                    clearance_id=data.get("clearanceId", ""),
                    clearance_hash=data.get("clearanceHash", ""),
                    timestamp=self._now_iso(),
                    status="cleared",
                    raw_response=data,
                )
            else:
                error_data = {}
                try:
                    error_data = response.json()
                except Exception:
                    pass
                return ClearanceResult(
                    success=False,
                    timestamp=self._now_iso(),
                    status="rejected",
                    errors=[f"ZATCA returned {response.status_code}: {error_data.get('errors', ['Unknown error'])}"],
                    raw_response=error_data,
                )
        except httpx.RequestError as e:
            logger.error(f"ZATCA submission failed: {e}")
            return ClearanceResult(
                success=False,
                timestamp=self._now_iso(),
                status="error",
                errors=[f"Network error: {str(e)}"],
            )

    async def check_status(self, clearance_id: str) -> ClearanceResult:
        """Check ZATCA clearance status."""
        if self.sandbox:
            return ClearanceResult(
                success=True,
                clearance_id=clearance_id,
                timestamp=self._now_iso(),
                status="cleared",
                raw_response={"note": "Sandbox — always cleared"},
            )

        client = await self._get_client()
        try:
            response = await client.get(
                f"{self.base_url}{self.ISSUE_INVOICE_PATH}/{clearance_id}",
                headers={"Authorization": f"Basic {self.api_key}"},
            )
            if response.status_code == 200:
                data = response.json()
                return ClearanceResult(
                    success=True,
                    clearance_id=clearance_id,
                    status=data.get("status", "unknown"),
                    timestamp=self._now_iso(),
                    raw_response=data,
                )
            return ClearanceResult(
                success=False,
                clearance_id=clearance_id,
                errors=[f"Status check failed: HTTP {response.status_code}"],
            )
        except httpx.RequestError as e:
            return ClearanceResult(
                success=False,
                clearance_id=clearance_id,
                errors=[f"Network error: {str(e)}"],
            )

    def _generate_qr_data(self, request: ClearanceRequest) -> str:
        """
        Generate ZATCA QR code TLV data (base64 encoded).

        TLV (Tag-Length-Value) fields:
        1: Seller name
        2: VAT registration number
        3: Invoice timestamp
        4: Total amount
        5: VAT amount
        """
        def tlv(tag: int, value: str) -> bytes:
            return bytes([tag]) + bytes([len(value)]) + value.encode("utf-8")

        fields = [
            tlv(1, request.vendor_name),
            tlv(2, request.vendor_tax_id),
            tlv(3, request.invoice_date or datetime.utcnow().isoformat()),
            tlv(4, f"{request.total_amount:.2f}"),
            tlv(5, f"{request.tax_amount:.2f}"),
        ]
        return base64.b64encode(b"".join(fields)).decode("utf-8")

    def _build_zatca_invoice(self, request: ClearanceRequest) -> dict:
        """Build the ZATCA-specific invoice structure."""
        return {
            "seller_name": request.vendor_name,
            "seller_vat": request.vendor_tax_id,
            "buyer_name": request.buyer_name,
            "buyer_vat": request.buyer_tax_id,
            "invoice_date": request.invoice_date,
            "total": request.total_amount,
            "vat": request.tax_amount,
            "currency": request.currency,
        }
