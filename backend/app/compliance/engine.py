"""Compliance Engine orchestrator.

Main entry point for all compliance operations. Routes invoices to the correct
compliance handler based on country_code and compliance_model.

Flow:
1. Determine compliance model from ComplianceConfig
2. Route to appropriate handler (PEPPOL / Clearance / CTC / Post-Audit)
3. Execute validation, submission, and archival
4. Return unified ComplianceResult
"""

import logging
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any

from app.compliance.peppol import UBLInvoiceGenerator, PEPPOLValidator, PEPPOLTransmitter, ValidationResult
from app.compliance.clearance import CLEARANCE_HANDLERS, ClearanceRequest, ClearanceResult
from app.compliance.ctc.reporter import CTC_REPORTERS, CTCReportRequest, CTCReportResult
from app.compliance.post_audit import PostAuditArchiver, ArchiveRequest, ArchiveResult

logger = logging.getLogger(__name__)


@dataclass
class ComplianceResult:
    """Unified result from the compliance engine."""
    success: bool = False
    model: str = ""
    country_code: str = ""
    status: str = "pending"
    clearance_id: str = ""
    validation: ValidationResult | None = None
    transmission: dict[str, Any] = field(default_factory=dict)
    archive: ArchiveResult | None = None
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    timestamp: str = ""


class ComplianceEngine:
    """
    Unified compliance engine.

    Routes invoices to the appropriate compliance handler based on
    the tenant's compliance configuration.
    """

    def __init__(
        self,
        sandbox: bool = True,
        peppol_customisation_id: str = "",
        zatca_api_key: str = "",
        zatca_api_secret: str = "",
        br_cnpj: str = "",
        in_gsp_api_key: str = "",
        mx_pac_api_key: str = "",
    ):
        self.sandbox = sandbox

        # PEPPOL
        self.peppol_generator = UBLInvoiceGenerator(customisation_id=peppol_customisation_id)
        self.peppol_validator = PEPPOLValidator()
        self.peppol_transmitter = PEPPOLTransmitter(sandbox=sandbox)

        # Clearance handlers
        self._clearance_handlers = {
            "SA": lambda: CLEARANCE_HANDLERS["SA"](sandbox=sandbox, api_key=zatca_api_key, api_secret=zatca_api_secret),
            "BR": lambda: CLEARANCE_HANDLERS["BR"](sandbox=sandbox, cnpj=br_cnpj),
            "IN": lambda: CLEARANCE_HANDLERS["IN"](sandbox=sandbox, gsp_api_key=in_gsp_api_key),
            "MX": lambda: CLEARANCE_HANDLERS["MX"](sandbox=sandbox, pac_api_key=mx_pac_api_key),
        }

        # CTC reporters
        self._ctc_reporters = {
            code: lambda c=code: CTC_REPORTERS[c](sandbox=sandbox)
            for code in CTC_REPORTERS
        }

        # Post-audit archiver
        self.archiver = PostAuditArchiver()

    async def process(
        self,
        invoice_data: dict[str, Any],
        compliance_model: str,
        country_code: str,
        lines: list[dict[str, Any]] | None = None,
    ) -> ComplianceResult:
        """
        Process an invoice through the compliance engine.

        Args:
            invoice_data: Invoice fields (must include invoice_number, vendor_name, etc.)
            compliance_model: "peppol", "clearance", "ctc", or "post_audit"
            country_code: ISO 3166-1 alpha-2 country code
            lines: Optional line items

        Returns:
            ComplianceResult with all details
        """
        result = ComplianceResult(
            model=compliance_model,
            country_code=country_code,
            timestamp=datetime.utcnow().isoformat() + "Z",
        )

        try:
            if compliance_model == "peppol":
                await self._process_peppol(invoice_data, lines, result)
            elif compliance_model == "clearance":
                await self._process_clearance(invoice_data, country_code, lines, result)
            elif compliance_model == "ctc":
                await self._process_ctc(invoice_data, country_code, lines, result)
            elif compliance_model == "post_audit":
                await self._process_post_audit(invoice_data, country_code, lines, result)
            else:
                result.errors.append(f"Unknown compliance model: {compliance_model}")
        except Exception as e:
            logger.error(f"Compliance processing failed: {e}")
            result.errors.append(f"Processing error: {str(e)}")
            result.status = "error"

        result.success = len(result.errors) == 0
        return result

    async def _process_peppol(
        self, invoice_data: dict, lines: list | None, result: ComplianceResult
    ) -> None:
        """Process invoice through PEPPOL compliance."""
        result.model = "peppol"

        # Step 1: Generate UBL 2.1 XML
        ubl_xml = self.peppol_generator.generate(invoice_data, lines)
        logger.info(f"Generated UBL XML for invoice {invoice_data.get('invoice_number')}")

        # Step 2: Validate
        validation = self.peppol_validator.validate_xml(ubl_xml)
        result.validation = validation

        if not validation.valid:
            result.errors.extend(validation.errors)
            result.warnings.extend(validation.warnings)
            result.status = "validation_failed"
            return

        result.warnings.extend(validation.warnings)

        # Step 3: Transmit
        sender_id = invoice_data.get("vendor_endpoint_id", "0088")
        recipient_id = invoice_data.get("buyer_endpoint_id", "0088")
        transmission = await self.peppol_transmitter.transmit(
            ubl_xml=ubl_xml,
            invoice_data=invoice_data,
            recipient_endpoint_id=recipient_id,
            sender_endpoint_id=sender_id,
        )

        result.transmission = {
            "message_id": transmission.message_id,
            "submission_id": transmission.submission_id,
            "status": transmission.status,
            "success": transmission.success,
            "raw_response": transmission.raw_response,
        }

        if transmission.success:
            result.status = "transmitted"
            result.clearance_id = transmission.submission_id
        else:
            result.status = "transmission_failed"
            result.errors.append(transmission.error)

    async def _process_clearance(
        self, invoice_data: dict, country_code: str, lines: list | None, result: ComplianceResult
    ) -> None:
        """Process invoice through country-specific clearance."""
        result.model = "clearance"
        result.country_code = country_code

        if country_code not in self._clearance_handlers:
            result.errors.append(f"No clearance handler configured for {country_code}")
            result.status = "unsupported_country"
            return

        # Build clearance request
        request = ClearanceRequest(
            invoice_number=invoice_data.get("invoice_number", ""),
            invoice_date=str(invoice_data.get("invoice_date", "")),
            vendor_name=invoice_data.get("vendor_name", ""),
            vendor_tax_id=invoice_data.get("vendor_tax_id", ""),
            buyer_name=invoice_data.get("buyer_name", ""),
            buyer_tax_id=invoice_data.get("buyer_tax_id", ""),
            currency=invoice_data.get("currency", ""),
            subtotal=invoice_data.get("subtotal", 0),
            tax_amount=invoice_data.get("tax_amount", 0),
            total_amount=invoice_data.get("total_amount", 0),
            country_code=country_code,
            lines=lines or invoice_data.get("lines", []),
            extra_fields=invoice_data.get("extra_fields", {}),
        )

        # Get handler and submit
        handler = self._clearance_handlers[country_code]()
        clearance_result = await handler.submit(request)

        result.transmission = clearance_result.raw_response
        if clearance_result.success:
            result.status = "cleared"
            result.clearance_id = clearance_result.clearance_id
            result.warnings.extend(clearance_result.warnings)
        else:
            result.status = "clearance_failed"
            result.errors.extend(clearance_result.errors)

    async def _process_ctc(
        self, invoice_data: dict, country_code: str, lines: list | None, result: ComplianceResult
    ) -> None:
        """Process invoice through CTC real-time reporting."""
        result.model = "ctc"
        result.country_code = country_code

        if country_code not in self._ctc_reporters:
            result.errors.append(f"No CTC reporter configured for {country_code}")
            result.status = "unsupported_country"
            return

        # Build CTC report request
        request = CTCReportRequest(
            invoice_number=invoice_data.get("invoice_number", ""),
            invoice_date=str(invoice_data.get("invoice_date", "")),
            vendor_name=invoice_data.get("vendor_name", ""),
            vendor_tax_id=invoice_data.get("vendor_tax_id", ""),
            buyer_name=invoice_data.get("buyer_name", ""),
            buyer_tax_id=invoice_data.get("buyer_tax_id", ""),
            currency=invoice_data.get("currency", ""),
            total_amount=invoice_data.get("total_amount", 0),
            tax_amount=invoice_data.get("tax_amount", 0),
            country_code=country_code,
            invoice_xml=invoice_data.get("invoice_xml", ""),
            lines=lines or invoice_data.get("lines", []),
            extra_fields=invoice_data.get("extra_fields", {}),
        )

        reporter = self._ctc_reporters[country_code]()
        ctc_result = await reporter.report(request)

        result.transmission = ctc_result.raw_response
        if ctc_result.success:
            result.status = "reported"
            result.clearance_id = ctc_result.report_id
            result.warnings.extend(ctc_result.warnings)
        else:
            result.status = "reporting_failed"
            result.errors.extend(ctc_result.errors)

    async def _process_post_audit(
        self, invoice_data: dict, country_code: str, lines: list | None, result: ComplianceResult
    ) -> None:
        """Process invoice for post-audit archival."""
        result.model = "post_audit"
        result.country_code = country_code

        request = ArchiveRequest(
            invoice_number=invoice_data.get("invoice_number", ""),
            invoice_date=str(invoice_data.get("invoice_date", "")),
            vendor_name=invoice_data.get("vendor_name", ""),
            vendor_tax_id=invoice_data.get("vendor_tax_id", ""),
            buyer_name=invoice_data.get("buyer_name", ""),
            buyer_tax_id=invoice_data.get("buyer_tax_id", ""),
            currency=invoice_data.get("currency", ""),
            total_amount=invoice_data.get("total_amount", 0),
            tax_amount=invoice_data.get("tax_amount", 0),
            country_code=country_code,
            invoice_xml=invoice_data.get("invoice_xml", ""),
            lines=lines or invoice_data.get("lines", []),
            retention_years=invoice_data.get("retention_years", 10),
            extra_fields=invoice_data.get("extra_fields", {}),
        )

        archive_result = await self.archiver.archive(request)

        if archive_result.success:
            result.status = "archived"
            result.clearance_id = archive_result.archive_id
            result.archive = archive_result
        else:
            result.status = "archive_failed"
            result.errors.extend(archive_result.errors)

    async def validate_only(
        self,
        invoice_data: dict[str, Any],
        compliance_model: str,
        country_code: str,
        lines: list[dict[str, Any]] | None = None,
    ) -> ComplianceResult:
        """
        Validate only (no submission). Useful for pre-checks.
        """
        result = ComplianceResult(
            model=compliance_model,
            country_code=country_code,
            timestamp=datetime.utcnow().isoformat() + "Z",
        )

        if compliance_model == "peppol":
            ubl_xml = self.peppol_generator.generate(invoice_data, lines)
            validation = self.peppol_validator.validate_xml(ubl_xml)
            result.validation = validation
            result.success = validation.valid
            if not validation.valid:
                result.errors.extend(validation.errors)
            result.warnings.extend(validation.warnings)
        elif compliance_model == "clearance":
            if country_code in self._clearance_handlers:
                request = ClearanceRequest(
                    invoice_number=invoice_data.get("invoice_number", ""),
                    invoice_date=str(invoice_data.get("invoice_date", "")),
                    vendor_name=invoice_data.get("vendor_name", ""),
                    vendor_tax_id=invoice_data.get("vendor_tax_id", ""),
                    buyer_name=invoice_data.get("buyer_name", ""),
                    buyer_tax_id=invoice_data.get("buyer_tax_id", ""),
                    currency=invoice_data.get("currency", ""),
                    subtotal=invoice_data.get("subtotal", 0),
                    tax_amount=invoice_data.get("tax_amount", 0),
                    total_amount=invoice_data.get("total_amount", 0),
                    country_code=country_code,
                    lines=lines or invoice_data.get("lines", []),
                    extra_fields=invoice_data.get("extra_fields", {}),
                )
                handler = self._clearance_handlers[country_code]()
                clearance_result = await handler.validate(request)
                result.success = clearance_result.success
                result.errors.extend(clearance_result.errors)
                result.warnings.extend(clearance_result.warnings)
        else:
            result.success = True

        result.status = "validated" if result.success else "validation_failed"
        return result
