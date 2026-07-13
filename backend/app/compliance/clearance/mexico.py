"""Mexico CFDI (Comprobante Fiscal Digital por Internet) clearance handler.

Mexico requires all business transactions to use CFDI — digital tax receipts
generated through an authorized PAC (Proveedor Autorizado de Certificación).

CFDI flow:
1. Generate XML per SAT (Servicio de Administración Tributaria) schema v4.0
2. Send to PAC for digital signature (stamp / timbrado)
3. Receive UUID (RFC fiscal folio), SAT signature, and QR code
4. Send CFDI XML to buyer as the official fiscal document
"""

import hashlib
import json
import logging
import uuid
from datetime import datetime
from typing import Any
from xml.etree.ElementTree import Element, SubElement, tostring

import httpx

from app.compliance.clearance.base import BaseClearance, ClearanceRequest, ClearanceResult

logger = logging.getLogger(__name__)


class MexicoCFDIClearance(BaseClearance):
    """Mexico CFDI 4.0 clearance handler."""

    country_code = "MX"
    country_name = "Mexico"
    clearance_model = "cfdi"

    # CFDI version
    CFDI_VERSION = "4.0"

    # Concepto (line item) types
    CLAVE_PRODUCTO_TYPES = {
        "product": "84111506",
        "service": "86121800",
        "digital": "82101500",
        "rent": "93151500",
        "other": "99999900",
    }

    # ClaveProdServ codes (simplified — real catalog has thousands)
    DEFAULT_CLAVE = "84111506"

    # Uso CFDI codes
    USO_CFDI = {
        "general": "G03",
        "credit_note": "G02",
        "debit_note": "G01",
        "export": "E01",
        "not_subject_to_tax": "CN01",
    }

    def __init__(self, sandbox: bool = True, pac_api_key: str = "", pac_api_secret: str = ""):
        self.sandbox = sandbox
        self.pac_api_key = pac_api_key
        self.pac_api_secret = pac_api_secret
        self._client: httpx.AsyncClient | None = None

    async def _get_client(self) -> httpx.AsyncClient:
        if self._client is None or self._client.is_closed:
            self._client = httpx.AsyncClient(timeout=30.0)
        return self._client

    async def validate(self, request: ClearanceRequest) -> ClearanceResult:
        """Validate invoice against CFDI rules."""
        errors = []
        warnings = []

        # RFC validation (Mexico tax ID)
        if not request.vendor_tax_id:
            errors.append("Emisor RFC is required")
        elif not self._validate_rfc(request.vendor_tax_id):
            errors.append("Invalid RFC format")

        if request.buyer_tax_id and not self._validate_rfc(request.buyer_tax_id):
            warnings.append("Receptor RFC format appears invalid")

        if request.currency not in ("MXN", "USD", "EUR", "JPY", "GBP", "CAD", "CHF"):
            warnings.append(f"Currency {request.currency} — verify it is in the SAT catalog")

        if request.total_amount <= 0:
            errors.append("Total amount must be positive")

        if not request.lines:
            errors.append("At least one concepto (line item) is required")

        # Validate CFDI use code
        uso_cfdi = request.extra_fields.get("uso_cfdi", "G03")
        if uso_cfdi not in self.USO_CFDI.values():
            warnings.append(f"Uso CFDI '{uso_cfdi}' may not be in the official catalog")

        for i, line in enumerate(request.lines, 1):
            if line.get("quantity", 0) <= 0:
                errors.append(f"Concepto {i}: Quantity must be positive")
            if line.get("unit_price", 0) < 0:
                errors.append(f"Concepto {i}: Unit price must not be negative")

        return ClearanceResult(
            success=len(errors) == 0,
            errors=errors,
            warnings=warnings,
            timestamp=self._now_iso(),
        )

    async def submit(self, request: ClearanceRequest) -> ClearanceResult:
        """Submit CFDI to PAC for timbrado (stamping)."""
        validation = await self.validate(request)
        if not validation.success:
            return validation

        # Build CFDI 4.0 XML
        cfdi_xml = self._build_cfdi_xml(request)

        if self.sandbox:
            return await self._sandbox_submit(cfdi_xml, request)

        return await self._production_submit(cfdi_xml, request)

    async def _sandbox_submit(self, cfdi_xml: str, request: ClearanceRequest) -> ClearanceResult:
        """Simulate PAC timbrado in sandbox."""
        logger.info(f"[CFDI SANDBOX] Simulating timbrado for {request.invoice_number}")

        import asyncio
        await asyncio.sleep(0.1)

        # Generate UUID (fiscal folio)
        fiscal_folio = str(uuid.uuid4()).upper()

        return ClearanceResult(
            success=True,
            clearance_id=fiscal_folio,
            clearance_hash=fiscal_folio[:16],
            timestamp=self._now_iso(),
            status="timbrado",
            raw_response={
                "status": "Aceptado",
                "uuid": fiscal_folio,
                "fechaTimbrado": datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S"),
                "noCertificadoSAT": "00001000000508762968",
                "rfcProvCertif": "CVD110412TF6",
                "selloCFD": hashlib.sha256(cfdi_xml.encode()).hexdigest()[:128],
                "selloSAT": hashlib.sha256(fiscal_folio.encode()).hexdigest()[:128],
                "qrCode": self._generate_qr_code(request, fiscal_folio),
                "note": "Sandbox mode — no real PAC timbrado",
            },
        )

    async def _production_submit(self, cfdi_xml: str, request: ClearanceRequest) -> ClearanceResult:
        """Submit to real PAC for timbrado."""
        logger.info(f"[CFDI] Submitting to PAC for timbrado: {request.invoice_number}")

        client = await self._get_client()
        try:
            response = await client.post(
                "https://api.pac.example.com/timbrar",
                content=cfdi_xml,
                headers={
                    "Authorization": f"Bearer {self.pac_api_key}",
                    "Content-Type": "application/xml",
                    "Accept": "application/json",
                },
            )

            if response.status_code == 200:
                data = response.json()
                return ClearanceResult(
                    success=True,
                    clearance_id=data.get("uuid", ""),
                    clearance_hash=data.get("uuid", "")[:16],
                    timestamp=self._now_iso(),
                    status="timbrado",
                    raw_response=data,
                )
            return ClearanceResult(
                success=False,
                timestamp=self._now_iso(),
                status="rejected",
                errors=[f"PAC returned HTTP {response.status_code}"],
            )
        except httpx.RequestError as e:
            logger.error(f"PAC timbrado failed: {e}")
            return ClearanceResult(
                success=False,
                timestamp=self._now_iso(),
                status="error",
                errors=[f"Network error: {str(e)}"],
            )

    async def check_status(self, clearance_id: str) -> ClearanceResult:
        """Check CFDI status (cancelled or active)."""
        if self.sandbox:
            return ClearanceResult(
                success=True,
                clearance_id=clearance_id,
                status="active",
                timestamp=self._now_iso(),
                raw_response={"note": "Sandbox — always active"},
            )
        return ClearanceResult(
            success=False,
            clearance_id=clearance_id,
            status="unknown",
            errors=["Production CFDI status check not yet implemented"],
        )

    def _build_cfdi_xml(self, request: ClearanceRequest) -> str:
        """
        Build CFDI 4.0 XML per SAT schema.

        Full implementation would use the official SAT XSD schemas.
        """
        cfdi_ns = "http://www.sat.gob.mx/cfd/4"
        tfd_ns = "http://www.sat.gob.mx/TimbreFiscalDigital"
        xsi_ns = "http://www.w3.org/2001/XMLSchema-instance"

        root = Element("cfdi:Comprobante")
        root.set("xmlns:cfdi", cfdi_ns)
        root.set("xmlns:xsi", xsi_ns)
        root.set("Version", self.CFDI_VERSION)
        root.set("Serie", request.extra_fields.get("serie", "A"))
        root.set("Folio", request.invoice_number)
        root.set("Fecha", datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S"))
        root.set("FormaPago", request.extra_fields.get("forma_pago", "03"))
        root.set("NoCertificado", request.extra_fields.get("no_certificado", "30001000000508762968"))
        root.set("Certificado", request.extra_fields.get("certificado", ""))
        root.set("SubTotal", f"{request.subtotal:.2f}")
        root.set("Moneda", request.currency)
        root.set("Total", f"{request.total_amount:.2f}")
        root.set("TipoDeComprobante", request.extra_fields.get("tipo_comprobante", "I"))
        root.set("MetodoPago", request.extra_fields.get("metodo_pago", "PUE"))
        root.set("LugarExpedicion", request.extra_fields.get("lugar_expedicion", "06600"))
        root.set("Exportacion", "01")
        root.set("Sello", hashlib.sha256(request.invoice_number.encode()).hexdigest()[:128])

        # Emisor (issuer)
        emisor = SubElement(root, "cfdi:Emisor")
        emisor.set("Rfc", self._clean_rfc(request.vendor_tax_id))
        emisor.set("Nombre", request.vendor_name)
        emisor.set("RegimenFiscal", request.extra_fields.get("regimen_fiscal", "601"))

        # Receptor (receiver)
        receptor = SubElement(root, "cfdi:Receptor")
        receptor.set("Rfc", self._clean_rfc(request.buyer_tax_id) if request.buyer_tax_id else "XAXX010101000")
        receptor.set("Nombre", request.buyer_name or "PUBLICO EN GENERAL")
        receptor.set("DomicilioFiscalReceptor", request.extra_fields.get("buyer_postal_code", "06600"))
        receptor.set("RegimenFiscalReceptor", request.extra_fields.get("buyer_regimen", "601"))
        receptor.set("UsoCFDI", request.extra_fields.get("uso_cfdi", "G03"))

        # Conceptos (line items)
        conceptos = SubElement(root, "cfdi:Conceptos")
        for i, line in enumerate(request.lines, 1):
            concepto = SubElement(conceptos, "cfdi:Concepto")
            concepto.set("ClaveProdServ", line.get("item_code", self.DEFAULT_CLAVE))
            concepto.set("NoIdentificacion", str(i))
            concepto.set("Cantidad", str(line.get("quantity", 1)))
            concepto.set("ClaveUnidad", line.get("unit_code", "E48"))
            concepto.set("Unidad", line.get("unit_name", "Pieza"))
            concepto.set("Descripcion", line.get("description", f"Item {i}"))
            concepto.set("ValorUnitario", f"{line.get('unit_price', 0):.2f}")
            concepto.set("Importe", f"{line.get('line_total', line.get('unit_price', 0) * line.get('quantity', 1)):.2f}")
            concepto.set("ObjetoImp", "02")

            # Impuestos (taxes) per line
            impuestos = SubElement(concepto, "cfdi:Impuestos")
            traslados = SubElement(impuestos, "cfdi:Traslados")
            traslado = SubElement(traslados, "cfdi:Traslado")
            traslado.set("Base", f"{line.get('line_total', 0):.2f}")
            traslado.set("Impuesto", "002")  # IVA
            traslado.set("TipoFactor", "Tasa")
            traslado.set("TasaOCuota", f"{line.get('tax_rate', 16) / 100:.6f}")
            traslado.set("Importe", f"{line.get('tax_amount', 0):.2f}")

        # Impuestos totales
        impuestos_totales = SubElement(root, "cfdi:Impuestos")
        impuestos_totales.set("TotalImpuestosTrasladados", f"{request.tax_amount:.2f}")
        traslados = SubElement(impuestos_totales, "cfdi:Traslados")
        traslado_total = SubElement(traslados, "cfdi:Traslado")
        traslado_total.set("Base", f"{request.subtotal:.2f}")
        traslado_total.set("Impuesto", "002")
        traslado_total.set("TipoFactor", "Tasa")
        traslado_total.set("TasaOCuota", "0.160000")
        traslado_total.set("Importe", f"{request.tax_amount:.2f}")

        # Complemento (placeholder for timbre fiscal digital)
        complemento = SubElement(root, "cfdi:Complemento")

        return '<?xml version="1.0" encoding="UTF-8"?>\n' + tostring(root, encoding="unicode")

    def _generate_qr_code(self, request: ClearanceRequest, uuid_str: str) -> str:
        """
        Generate CFDI QR code data string.

        The QR code encodes: RFC emisor|RFC receptor|total|uuid|no_certificado_sat|fecha_timbrado|sello_sat
        """
        parts = [
            self._clean_rfc(request.vendor_tax_id),
            self._clean_rfc(request.buyer_tax_id) if request.buyer_tax_id else "XAXX010101000",
            f"{request.total_amount:.2f}",
            uuid_str,
            "00001000000508762968",  # noCertificadoSAT
            datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S"),
            hashlib.sha256(uuid_str.encode()).hexdigest()[:128],
        ]
        return "|".join(parts)

    @staticmethod
    def _validate_rfc(rfc: str) -> bool:
        """Validate Mexican RFC (tax identification number)."""
        clean = rfc.upper().strip()
        # Persona moral: 12 chars, Persona física: 13 chars
        if len(clean) == 12:
            return True  # Simplified validation
        elif len(clean) == 13:
            return True
        return False

    @staticmethod
    def _clean_rfc(rfc: str) -> str:
        """Clean RFC to uppercase alphanumeric only."""
        return "".join(c for c in rfc.upper() if c.isalnum())
