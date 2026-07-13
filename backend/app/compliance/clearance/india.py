"""India IRP (Invoice Registration Portal) clearance handler.

India requires B2B invoices above ₹500 to be registered on the IRP (e-invoice system)
managed by the GST Network (GSTN). The IRP validates the invoice, generates an
Invoice Reference Number (IRN), and signs it with a Digital Signature (DSC).

IRP flow:
1. Generate JSON payload per GSTN schema
2. Submit to IRP via registered GSP/ASP
3. Receive IRN + signed QR code + digital signature
4. Print IRN and QR code on the invoice
"""

import hashlib
import json
import logging
import uuid
from datetime import datetime
from typing import Any

import httpx

from app.compliance.clearance.base import BaseClearance, ClearanceRequest, ClearanceResult

logger = logging.getLogger(__name__)


class IndiaIRPClearance(BaseClearance):
    """India GST e-Invoice IRP clearance handler."""

    country_code = "IN"
    country_name = "India"
    clearance_model = "irp"

    # IRP endpoints (sandbox/sandbox)
    SANDBOX_URL = "https://sandbox.einvoice1.gov.in"
    PRODUCTION_URL = "https://einvoice1.gov.in"
    IRP_ENDPOINT = "/eapi/v1.04/invoice"

    # GSTIN validation pattern
    GSTIN_PATTERN = r"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$"

    # Supply type codes
    SUPPLY_TYPES = {
        "regular": " taxable",
        "export": "SKD/CKD/Lots",
        "sez": "SEZ",
        "deemed": "DE",
        "exempt": "EXEMPT",
        "nil_rated": "NIL",
    }

    # Invoice type codes
    INVOICE_TYPES = {
        "regular": "R",
        "credit_note": "CR",
        "debit_note": "DR",
        "export": "EXP",
        "sez": "SEZ",
    }

    def __init__(self, sandbox: bool = True, gsp_api_key: str = "", gsp_api_secret: str = ""):
        self.sandbox = sandbox
        self.gsp_api_key = gsp_api_key
        self.gsp_api_secret = gsp_api_secret
        self.base_url = self.SANDBOX_URL if sandbox else self.PRODUCTION_URL
        self._client: httpx.AsyncClient | None = None

    async def _get_client(self) -> httpx.AsyncClient:
        if self._client is None or self._client.is_closed:
            self._client = httpx.AsyncClient(timeout=30.0)
        return self._client

    async def validate(self, request: ClearanceRequest) -> ClearanceResult:
        """Validate invoice against Indian IRP rules."""
        import re
        errors = []
        warnings = []

        # GSTIN validation
        if not request.vendor_tax_id:
            errors.append("Supplier GSTIN is required")
        elif not re.match(self.GSTIN_PATTERN, request.vendor_tax_id):
            errors.append("Invalid GSTIN format (must be 15 characters: 2 digits + 5 letters + 4 digits + 1 letter + Z + 1 alphanumeric)")

        if request.buyer_tax_id and not re.match(self.GSTIN_PATTERN, request.buyer_tax_id):
            warnings.append("Buyer GSTIN format appears invalid")

        if request.currency != "INR":
            warnings.append(f"Currency {request.currency} will be converted to INR for e-invoicing")

        if request.total_amount <= 0:
            errors.append("Total amount must be positive")

        # IRP has a minimum threshold of ₹500 for B2B
        if request.total_amount < 500 and not request.extra_fields.get("force_irn"):
            warnings.append(f"Invoice amount ₹{request.total_amount} is below ₹500 IRP threshold — IRN not mandatory")

        if not request.lines:
            errors.append("At least one line item is required")

        # Validate HSN codes (if provided)
        for i, line in enumerate(request.lines, 1):
            hsn = line.get("item_code", "")
            if hsn and len(hsn) < 4:
                warnings.append(f"Line {i}: HSN code '{hsn}' should be at least 4 digits")

        # CGST/SGST vs IGST validation (simplified)
        place_of_supply = request.extra_fields.get("place_of_supply", "")
        if place_of_supply and place_of_supply[:2] != request.vendor_tax_id[:2]:
            # Inter-state supply — should use IGST
            for i, line in enumerate(request.lines, 1):
                if line.get("cgst_rate") and not line.get("igst_rate"):
                    warnings.append(f"Line {i}: Inter-state supply should use IGST instead of CGST+SGST")

        return ClearanceResult(
            success=len(errors) == 0,
            errors=errors,
            warnings=warnings,
            timestamp=self._now_iso(),
        )

    async def submit(self, request: ClearanceRequest) -> ClearanceResult:
        """Submit invoice to IRP for IRN generation."""
        validation = await self.validate(request)
        if not validation.success:
            return validation

        # Build IRP JSON payload
        irp_payload = self._build_irp_payload(request)

        if self.sandbox:
            return await self._sandbox_submit(irp_payload, request)

        return await self._production_submit(irp_payload, request)

    async def _sandbox_submit(self, payload: dict, request: ClearanceRequest) -> ClearanceResult:
        """Simulate IRP registration in sandbox."""
        logger.info(f"[IRP SANDBOX] Simulating IRN generation for invoice {request.invoice_number}")

        import asyncio
        await asyncio.sleep(0.1)

        # Generate deterministic IRN (hash of key fields)
        irn_source = f"{request.invoice_number}{request.invoice_date}{request.vendor_tax_id}{request.total_amount}"
        irn = hashlib.sha256(irn_source.encode()).hexdigest().upper()[:64]

        # QR code data (IRN + invoice details)
        qr_data = {
            "IRN": irn,
            "Amt": f"{request.total_amount:.2f}",
            "DT": request.invoice_date or datetime.utcnow().strftime("%d/%m/%Y"),
            "TXPD": request.vendor_tax_id,
        }

        return ClearanceResult(
            success=True,
            clearance_id=irn,
            clearance_hash=irn[:32],
            timestamp=self._now_iso(),
            status="registered",
            raw_response={
                "status": "Success",
                "irn": irn,
                "ackNo": f"IRN{uuid.uuid4().hex[:14]}",
                "ackDt": datetime.utcnow().strftime("%d/%m/%Y %H:%M:%S"),
                "signedQRCode": json.dumps(qr_data),
                "note": "Sandbox mode — no real IRP submission",
            },
        )

    async def _production_submit(self, payload: dict, request: ClearanceRequest) -> ClearanceResult:
        """Submit to real IRP via GSP/ASP."""
        logger.info(f"[IRP] Submitting invoice {request.invoice_number} for IRN")

        client = await self._get_client()
        try:
            response = await client.post(
                f"{self.base_url}{self.IRP_ENDPOINT}",
                json={"data": payload},
                headers={
                    "gstin": self.gsp_api_key,
                    "user_name": request.vendor_tax_id,
                    "ip_address": "0.0.0.0",
                    "Content-Type": "application/json",
                },
            )

            if response.status_code == 200:
                data = response.json()
                if data.get("success") == "Y" or data.get("ewayBillNo"):
                    return ClearanceResult(
                        success=True,
                        clearance_id=data.get("irn", ""),
                        clearance_hash=data.get("irn", "")[:32],
                        timestamp=self._now_iso(),
                        status="registered",
                        raw_response=data,
                    )
                else:
                    return ClearanceResult(
                        success=False,
                        timestamp=self._now_iso(),
                        status="rejected",
                        errors=[data.get("ErrorDetails", {}).get("ErrorMessage", "IRP rejection")],
                        raw_response=data,
                    )
            return ClearanceResult(
                success=False,
                timestamp=self._now_iso(),
                status="error",
                errors=[f"IRP returned HTTP {response.status_code}"],
            )
        except httpx.RequestError as e:
            logger.error(f"IRP submission failed: {e}")
            return ClearanceResult(
                success=False,
                timestamp=self._now_iso(),
                status="error",
                errors=[f"Network error: {str(e)}"],
            )

    async def check_status(self, clearance_id: str) -> ClearanceResult:
        """Check IRN status on IRP."""
        if self.sandbox:
            return ClearanceResult(
                success=True,
                clearance_id=clearance_id,
                status="registered",
                timestamp=self._now_iso(),
                raw_response={"note": "Sandbox — always registered"},
            )
        return ClearanceResult(
            success=False,
            clearance_id=clearance_id,
            status="unknown",
            errors=["Production IRN status check not yet implemented"],
        )

    def _build_irp_payload(self, request: ClearanceRequest) -> dict:
        """
        Build IRP-compatible JSON payload per GSTN schema v1.04.

        The IRP requires a specific JSON structure with headers, docDetails,
        sellerDtls, buyerDtls, itemList, etc.
        """
        invoice_type = request.extra_fields.get("type", "regular")
        place_of_supply = request.extra_fields.get("place_of_supply", request.buyer_tax_id[:2] if request.buyer_tax_id else "01")

        payload = {
            "Version": "1.04",
            "TranDtls": {
                "TaxSch": "GST",
                "SupTyp": self.SUPPLY_TYPES.get(invoice_type, " taxable"),
                "RegRev": "N",
                "Igstm": "N",
            },
            "DocDtls": {
                "Typ": self.INVOICE_TYPES.get(invoice_type, "R"),
                "No": request.invoice_number,
                "Dt": request.invoice_date or datetime.utcnow().strftime("%d/%m/%Y"),
            },
            "SellerDtls": {
                "Gstin": request.vendor_tax_id,
                "LglNm": request.vendor_name,
                "Addr1": request.extra_fields.get("seller_address", ""),
                "Loc": request.extra_fields.get("seller_city", ""),
                "Pin": request.extra_fields.get("seller_pin", 0),
                "Stcd": request.vendor_tax_id[:2] if len(request.vendor_tax_id) >= 2 else "01",
            },
            "BuyerDtls": {
                "Gstin": request.buyer_tax_id or "URP",
                "LglNm": request.buyer_name,
                "Addr1": request.extra_fields.get("buyer_address", ""),
                "Loc": request.extra_fields.get("buyer_city", ""),
                "Pin": request.extra_fields.get("buyer_pin", 0),
                "Stcd": place_of_supply[:2] if place_of_supply else "01",
                "Pos": place_of_supply[:2] if place_of_supply else "01",
            },
            "ItemList": [],
            "ValDtls": {
                "AssVal": request.subtotal,
                "CgstVal": request.extra_fields.get("cgst_amount", 0),
                "SgstVal": request.extra_fields.get("sgst_amount", 0),
                "IgstVal": request.extra_fields.get("igst_amount", request.tax_amount),
                "OthChrg": request.extra_fields.get("other_charges", 0),
                "RndOffAmt": 0,
                "TotInvVal": request.total_amount,
            },
        }

        # Build item list
        for i, line in enumerate(request.lines, 1):
            hsn_code = line.get("item_code", "9999")
            is_service = line.get("is_service", False)
            unit_code = "NOS" if not is_service else "SVC"

            qty = line.get("quantity", 1)
            unit_price = line.get("unit_price", 0)
            taxable_value = qty * unit_price

            item = {
                "SlNo": str(i),
                "PrdDesc": line.get("description", f"Item {i}"),
                "IsServc": "Y" if is_service else "N",
                "HsnCd": hsn_code if len(hsn_code) >= 4 else hsn_code.ljust(4, "0"),
                "Qty": qty,
                "Unit": unit_code,
                "UnitPrice": unit_price,
                "TotAmt": taxable_value,
                "AssAmt": taxable_value,
                "GstRt": line.get("tax_rate", 18),
                "IgstAmt": line.get("igst_amount", 0),
                "CgstAmt": line.get("cgst_amount", 0),
                "SgstAmt": line.get("sgst_amount", 0),
                "CesRt": 0,
                "CesAmt": 0,
                "CesNonAdvlAmt": 0,
                "StateCesRt": 0,
                "StateCesAmt": 0,
                "StateCesNonAdvlAmt": 0,
                "TotItemVal": taxable_value + line.get("igst_amount", 0) + line.get("cgst_amount", 0) + line.get("sgst_amount", 0),
            }
            payload["ItemList"].append(item)

        return payload
