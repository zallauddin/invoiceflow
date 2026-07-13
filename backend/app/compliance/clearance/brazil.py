"""Brazil NFe (Nota Fiscal Eletrônica) clearance handler.

Brazil requires electronic fiscal documents (NF-e) for B2B transactions,
cleared through the SEFAZ (Secretaria da Fazenda) state tax authority.

Integration:
- Produces NF-e XML in the standard Brazilian fiscal format
- Submits via SEFAZ web service for authorization
- Receives authorization protocol number and digital signature
"""

import hashlib
import logging
import uuid
from datetime import datetime
from typing import Any
from xml.etree.ElementTree import Element, SubElement, tostring

import httpx

from app.compliance.clearance.base import BaseClearance, ClearanceRequest, ClearanceResult

logger = logging.getLogger(__name__)


class BrazilNFeClearance(BaseClearance):
    """Brazil NF-e clearance handler."""

    country_code = "BR"
    country_name = "Brazil"
    clearance_model = "nfe"

    # SEFAZ endpoints (Sandbox)
    SANDBOX_URL = "https://homologacao.nfe.fazenda.gov.br"
    PRODUCTION_URL = "https://nfe.fazenda.gov.br"
    NFE_WS_PATH = "/NfeService4/NfeAutorizacao"

    # Brazilian CST (Código de Situação Tributária) codes
    CST_CODES = {
        "00": "Tributado integralmente",
        "10": "Tributado e com ICMS-ST",
        "20": "Com redução de base de cálculo",
        "30": "Isento ou não tributado e com ICMS-ST",
        "40": "Isento",
        "41": "Não tributado",
        "50": "Suspensão",
        "60": "ICMS cobrado anteriormente",
        "70": "Redução de base de cálculo e cobrança de ICMS-ST",
        "90": "Outros",
    }

    def __init__(self, sandbox: bool = True, cnpj: str = "", cert_path: str = "", cert_password: str = ""):
        self.sandbox = sandbox
        self.cnpj = cnpj
        self.cert_path = cert_path
        self.cert_password = cert_password
        self.base_url = self.SANDBOX_URL if sandbox else self.PRODUCTION_URL
        self._client: httpx.AsyncClient | None = None

    async def _get_client(self) -> httpx.AsyncClient:
        if self._client is None or self._client.is_closed:
            self._client = httpx.AsyncClient(timeout=30.0)
        return self._client

    async def validate(self, request: ClearanceRequest) -> ClearanceResult:
        """Validate invoice against Brazilian NFe rules."""
        errors = []
        warnings = []

        # CNPJ validation (14 digits)
        if not request.vendor_tax_id:
            errors.append("Seller CNPJ is required")
        elif not self._validate_cnpj(request.vendor_tax_id):
            errors.append("Invalid CNPJ format (must be 14 digits)")

        if request.buyer_tax_id and not self._validate_cnpj(request.buyer_tax_id):
            warnings.append("Buyer CNPJ format appears invalid")

        if request.currency != "BRL":
            warnings.append(f"Currency {request.currency} will be converted to BRL for NFe")

        if request.total_amount <= 0:
            errors.append("Total amount must be positive")

        if not request.lines:
            errors.append("At least one product/service line is required")

        # Validate tax rates (Brazil ICMS varies by state)
        for i, line in enumerate(request.lines, 1):
            tax_rate = line.get("tax_rate", 0)
            if tax_rate < 0 or tax_rate > 25:
                warnings.append(f"Line {i}: ICMS rate {tax_rate}% seems unusual for Brazil")

        return ClearanceResult(
            success=len(errors) == 0,
            errors=errors,
            warnings=warnings,
            timestamp=self._now_iso(),
        )

    async def submit(self, request: ClearanceRequest) -> ClearanceResult:
        """Submit NF-e for SEFAZ authorization."""
        validation = await self.validate(request)
        if not validation.success:
            return validation

        # Build NF-e XML
        nfe_xml = self._build_nfe_xml(request)

        if self.sandbox:
            return await self._sandbox_submit(nfe_xml, request)

        return await self._production_submit(nfe_xml, request)

    async def _sandbox_submit(self, nfe_xml: str, request: ClearanceRequest) -> ClearanceResult:
        """Simulate SEFAZ authorization in sandbox."""
        logger.info(f"[NFe SANDBOX] Simulating authorization for NF-e {request.invoice_number}")

        import asyncio
        await asyncio.sleep(0.1)

        nfe_number = f"{request.extra_fields.get('nfe_series', 1):03d}-{request.invoice_number}"
        protocol_number = f"352{uuid.uuid4().hex[:13]}"
        nfe_hash = hashlib.sha256(nfe_xml.encode()).hexdigest()[:44]

        return ClearanceResult(
            success=True,
            clearance_id=f"NFe-{nfe_number}",
            clearance_hash=nfe_hash,
            timestamp=self._now_iso(),
            status="authorized",
            raw_response={
                "status": "Autorizado",
                "nfe_number": nfe_number,
                "protocol_number": protocol_number,
                "nfe_key": nfe_hash,
                "xml_digest": nfe_hash,
                "note": "Sandbox mode — no real SEFAZ submission",
            },
        )

    async def _production_submit(self, nfe_xml: str, request: ClearanceRequest) -> ClearanceResult:
        """Submit to real SEFAZ for authorization."""
        logger.info(f"[NFe] Submitting NF-e {request.invoice_number} for SEFAZ authorization")

        client = await self._get_client()
        try:
            response = await client.post(
                f"{self.base_url}{self.NFE_WS_PATH}",
                content=nfe_xml,
                headers={"Content-Type": "application/xml"},
            )

            if response.status_code == 200:
                data = response.text
                # Parse SEFAZ response (simplified)
                return ClearanceResult(
                    success=True,
                    clearance_id=f"NFe-{request.invoice_number}",
                    clearance_hash=hashlib.sha256(data.encode()).hexdigest()[:44],
                    timestamp=self._now_iso(),
                    status="authorized",
                    raw_response={"sefaz_response": data[:500]},
                )
            return ClearanceResult(
                success=False,
                timestamp=self._now_iso(),
                status="rejected",
                errors=[f"SEFAZ returned HTTP {response.status_code}"],
            )
        except httpx.RequestError as e:
            logger.error(f"NFe submission failed: {e}")
            return ClearanceResult(
                success=False,
                timestamp=self._now_iso(),
                status="error",
                errors=[f"Network error: {str(e)}"],
            )

    async def check_status(self, clearance_id: str) -> ClearanceResult:
        """Check NF-e authorization status."""
        if self.sandbox:
            return ClearanceResult(
                success=True,
                clearance_id=clearance_id,
                status="authorized",
                timestamp=self._now_iso(),
                raw_response={"note": "Sandbox — always authorized"},
            )
        # Production would query SEFAZ by nfe_key
        return ClearanceResult(
            success=False,
            clearance_id=clearance_id,
            status="unknown",
            errors=["Production status check not yet implemented"],
        )

    def _build_nfe_xml(self, request: ClearanceRequest) -> str:
        """
        Build Brazilian NFe XML.

        Uses the standard NFe XML schema structure.
        Full implementation would use the official SEFAZ XSD schemas.
        """
        root = Element("NFe")
        inf_nfe = SubElement(root, "infNFe", versao="4.00")
        inf_nfe.set("Id", f"NFe{request.extra_fields.get('nfe_key', uuid.uuid4().hex[:44])}")

        # Identification
        ide = SubElement(inf_nfe, "ide")
        SubElement(ide, "cUF").text = "35"  # São Paulo
        SubElement(ide, "natOp").text = "Venda de mercadoria"
        SubElement(ide, "mod").text = "55"  # Model 55 = NFe
        SubElement(ide, "serie").text = "1"
        SubElement(ide, "nNF").text = request.invoice_number
        SubElement(ide, "tpEmis").text = "1"  # Normal emission

        # Supplier (emitente)
        emit = SubElement(inf_nfe, "emit")
        SubElement(emit, "CNPJ").text = self._clean_tax_id(request.vendor_tax_id)
        SubElement(emit, "xNome").text = request.vendor_name
        SubElement(emit, "xFant").text = request.vendor_name

        dest = SubElement(inf_nfe, "dest")
        if request.buyer_tax_id:
            SubElement(dest, "CNPJ").text = self._clean_tax_id(request.buyer_tax_id)
        SubElement(dest, "xNome").text = request.buyer_name

        # Items (det)
        for i, line in enumerate(request.lines, 1):
            det = SubElement(inf_nfe, "det", nItem=str(i))
            prod = SubElement(det, "prod")
            SubElement(prod, "cProd").text = line.get("item_code", f"{i:03d}")
            SubElement(prod, "xProd").text = line.get("description", f"Item {i}")
            SubElement(prod, "uCom").text = "UN"
            SubElement(prod, "qCom").text = f"{line.get('quantity', 1):.4f}"
            SubElement(prod, "vUnCom").text = f"{line.get('unit_price', 0):.10f}"
            SubElement(prod, "vProd").text = f"{line.get('line_total', 0):.2f}"

            # ICMS tax
            icms = SubElement(det, "imposto")
            icms_det = SubElement(icms, "ICMS")
            SubElement(icms_det, "orig").text = "0"
            SubElement(icms_det, "CST").text = "00"
            SubElement(icms_det, "modBC").text = "0"
            SubElement(icms_det, "vBC").text = f"{line.get('line_total', 0):.2f}"
            SubElement(icms_det, "pICMS").text = f"{line.get('tax_rate', 18):.2f}"
            SubElement(icms_det, "vICMS").text = f"{line.get('tax_amount', 0):.2f}"

        # Totals
        total = SubElement(inf_nfe, "total")
        icmstot = SubElement(total, "ICMSTot")
        SubElement(icmstot, "vNF").text = f"{request.total_amount:.2f}"
        SubElement(icmstot, "vICMS").text = f"{request.tax_amount:.2f}"
        SubElement(icmstot, "vBC").text = f"{request.subtotal:.2f}"

        return tostring(root, encoding="unicode", xml_declaration=True)

    @staticmethod
    def _validate_cnpj(cnpj: str) -> bool:
        """Validate Brazilian CNPJ (14 digits)."""
        clean = "".join(filter(str.isdigit, cnpj))
        if len(clean) != 14:
            return False
        # Basic length check (full digit validation would be more complex)
        return True

    @staticmethod
    def _clean_tax_id(tax_id: str) -> str:
        """Remove non-numeric characters from tax ID."""
        return "".join(filter(str.isdigit, tax_id))
