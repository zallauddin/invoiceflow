"""Extraction pipeline — orchestrates OCR, LLM fallback, and XML parsing.

Flow:
1. If PDF, try XML extraction first (ZUGFeRD/Factur-X/UBL/CII)
2. If XML extracted successfully → done
3. Run OCR on the document
4. If OCR confidence >= threshold (0.85) → use OCR result
5. If OCR confidence < threshold → LLM fallback
6. Merge results and update Invoice record
"""

import logging
import uuid
from datetime import datetime

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.config import settings
from app.extraction.ocr import OCRExtractor, OCRResult
from app.extraction.llm import LLMExtractor, LLMResult
from app.extraction.xml_parser import XMLInvoiceParser, XMLExtractionResult
from app.ingestion.storage import StorageManager
from app.models.invoice import (
    Invoice,
    InvoiceStatus,
    ExtractionMethod,
    InvoiceLine,
)
from app.models.audit import AuditLog

logger = logging.getLogger(__name__)

# Lazy-init singletons
_ocr: OCRExtractor | None = None
_llm: LLMExtractor | None = None
_xml_parser: XMLInvoiceParser | None = None
_storage: StorageManager | None = None


def _get_ocr() -> OCRExtractor:
    global _ocr
    if _ocr is None:
        _ocr = OCRExtractor()
    return _ocr


def _get_llm() -> LLMExtractor:
    global _llm
    if _llm is None:
        _llm = LLMExtractor()
    return _llm


def _get_xml_parser() -> XMLInvoiceParser:
    global _xml_parser
    if _xml_parser is None:
        _xml_parser = XMLInvoiceParser()
    return _xml_parser


def _get_storage() -> StorageManager:
    global _storage
    if _storage is None:
        _storage = StorageManager()
    return _storage


class ExtractionPipeline:
    """Orchestrate multi-method invoice extraction."""

    async def extract_invoice(
        self,
        db: AsyncSession,
        invoice_id: uuid.UUID,
        file_bytes: bytes | None = None,
        mime_type: str | None = None,
    ) -> Invoice:
        """Run extraction pipeline on an invoice.

        If file_bytes is not provided, downloads from MinIO storage.
        """
        # Load invoice
        result = await db.execute(
            select(Invoice).where(Invoice.id == invoice_id)
        )
        invoice = result.scalar_one_or_none()
        if not invoice:
            raise ValueError(f"Invoice {invoice_id} not found")

        # Update status
        invoice.status = InvoiceStatus.PROCESSING
        invoice.processing_started_at = datetime.utcnow()
        await db.flush()

        # Download from storage if needed
        if file_bytes is None:
            storage = _get_storage()
            if not invoice.file_url:
                raise ValueError(f"Invoice {invoice_id} has no file_url")
            file_bytes = await storage.download_file(invoice.file_url)
            mime_type = invoice.mime_type or "application/pdf"

        # Log start
        self._add_audit(
            db, invoice, "extraction_started",
            {"mime_type": mime_type, "file_size": len(file_bytes)},
        )

        try:
            extraction_result = None
            extraction_method = None

            # Step 1: Try XML extraction for PDFs
            if mime_type and "pdf" in mime_type.lower():
                xml_parser = _get_xml_parser()
                xml_result = xml_parser.parse_pdf(file_bytes)
                if xml_result and xml_result.success:
                    extraction_result = xml_result
                    extraction_method = ExtractionMethod.XML
                    logger.info(
                        f"Invoice {invoice_id}: XML extraction successful "
                        f"(format={xml_result.format})"
                    )

            # Step 2: OCR extraction
            if extraction_result is None:
                ocr = _get_ocr()
                ocr_result = ocr.extract(file_bytes, mime_type or "image/png")
                extraction_result = ocr_result
                extraction_method = ExtractionMethod.OCR

                logger.info(
                    f"Invoice {invoice_id}: OCR extraction complete "
                    f"(confidence={ocr_result.confidence:.2f})"
                )

                # Step 3: LLM fallback if confidence low
                if ocr_result.confidence < settings.LLM_CONFIDENCE_THRESHOLD:
                    logger.info(
                        f"Invoice {invoice_id}: Low confidence "
                        f"({ocr_result.confidence:.2f} < {settings.LLM_CONFIDENCE_THRESHOLD}), "
                        f"trying LLM fallback"
                    )
                    llm = _get_llm()

                    # Try image-based extraction first, fall back to text
                    if mime_type and (
                        "image" in mime_type or "pdf" in mime_type
                    ):
                        llm_result = llm.extract_from_image(
                            file_bytes, mime_type
                        )
                    else:
                        llm_result = llm.extract_from_text(
                            ocr_result.raw_text
                        )

                    if llm_result.success:
                        extraction_result = llm_result
                        extraction_method = ExtractionMethod.LLM
                        logger.info(
                            f"Invoice {invoice_id}: LLM extraction successful "
                            f"(provider={llm_result.provider})"
                        )
                    else:
                        logger.warning(
                            f"Invoice {invoice_id}: LLM extraction failed "
                            f"({llm_result.error}), using OCR result"
                        )

            # Step 4: Update invoice with extracted data
            self._apply_extraction(invoice, extraction_result, extraction_method)

            # Step 5: Create invoice lines
            self._create_line_items(invoice, extraction_result)

            # Step 6: Calculate totals if missing
            self._calculate_totals(invoice)

            # Update status
            invoice.status = InvoiceStatus.EXTRACTED
            invoice.processing_completed_at = datetime.utcnow()

            # Log completion
            self._add_audit(
                db, invoice, "extraction_completed",
                {
                    "method": extraction_method.value if extraction_method else "unknown",
                    "confidence": getattr(extraction_result, "confidence", None),
                    "fields_extracted": list(
                        k for k in vars(extraction_result)
                        if k not in ("raw_text", "raw_xml", "line_items", "metadata", "success", "error")
                        and getattr(extraction_result, k, None) is not None
                    ),
                },
            )

            await db.flush()
            logger.info(
                f"Invoice {invoice_id} extraction complete: "
                f"method={extraction_method.value if extraction_method else 'none'}, "
                f"status={invoice.status.value}"
            )

            return invoice

        except Exception as e:
            invoice.status = InvoiceStatus.FAILED
            invoice.error_message = str(e)
            invoice.processing_completed_at = datetime.utcnow()

            self._add_audit(
                db, invoice, "extraction_failed",
                {"error": str(e)},
            )

            await db.flush()
            logger.error(f"Invoice {invoice_id} extraction failed: {e}")
            return invoice

    def _apply_extraction(
        self,
        invoice: Invoice,
        result,
        method: ExtractionMethod | None,
    ):
        """Apply extraction results to invoice fields."""
        if result is None:
            return

        invoice.extraction_method = method

        # Get parsed data (works for OCR, LLM, and XML results)
        if isinstance(result, OCRResult):
            data = {
                "invoice_number": result.invoice_number,
                "invoice_date": result.invoice_date,
                "due_date": result.due_date,
                "vendor_name": result.vendor_name,
                "vendor_tax_id": result.vendor_tax_id,
                "buyer_name": result.buyer_name,
                "currency": result.currency,
                "subtotal": result.subtotal,
                "tax_amount": result.tax_amount,
                "total_amount": result.total_amount,
                "confidence": result.confidence,
                "raw_text": result.raw_text[:5000] if result.raw_text else None,
            }
            invoice.ocr_confidence = result.confidence
        elif isinstance(result, LLMResult):
            data = result.parsed_data
        elif isinstance(result, XMLExtractionResult):
            data = {
                "invoice_number": result.invoice_number,
                "invoice_date": result.invoice_date,
                "due_date": result.due_date,
                "vendor_name": result.vendor_name,
                "vendor_tax_id": result.vendor_tax_id,
                "buyer_name": result.buyer_name,
                "currency": result.currency,
                "subtotal": result.subtotal,
                "tax_amount": result.tax_amount,
                "total_amount": result.total_amount,
                "format": result.format,
                "raw_xml": result.raw_xml[:5000] if result.raw_xml else None,
            }
            invoice.ocr_confidence = 1.0  # XML is authoritative
        else:
            data = {}

        # Update invoice fields (only if extracted data has a value)
        if data.get("invoice_number") and data["invoice_number"] != "Pending extraction":
            invoice.invoice_number = data["invoice_number"]
        if data.get("vendor_name") and data["vendor_name"] != "Pending extraction":
            invoice.vendor_name = data["vendor_name"]
        if data.get("vendor_tax_id"):
            invoice.vendor_tax_id = data["vendor_tax_id"]
        if data.get("buyer_name"):
            invoice.buyer_name = data["buyer_name"]
        if data.get("buyer_tax_id"):
            invoice.buyer_tax_id = data["buyer_tax_id"]
        if data.get("currency"):
            invoice.currency = data["currency"]
        if data.get("subtotal") is not None:
            invoice.subtotal = float(data["subtotal"])
        if data.get("tax_amount") is not None:
            invoice.tax_amount = float(data["tax_amount"])
        if data.get("total_amount") is not None:
            invoice.total_amount = float(data["total_amount"])

        # Parse dates
        if data.get("invoice_date"):
            invoice.invoice_date = self._parse_date(data["invoice_date"])
        if data.get("due_date"):
            invoice.due_date = self._parse_date(data["due_date"])

        # Store all extracted data
        invoice.extracted_data = data

    def _create_line_items(self, invoice: Invoice, result):
        """Create InvoiceLine records from extracted line items."""
        if result is None:
            return

        # Get line items from result
        line_items = []
        if isinstance(result, OCRResult):
            line_items = result.line_items
        elif isinstance(result, LLMResult):
            line_items = result.parsed_data.get("line_items", [])
        elif isinstance(result, XMLExtractionResult):
            line_items = result.line_items

        if not line_items:
            return

        # Clear existing lines (in case of re-extraction)
        invoice.lines.clear()

        for i, item in enumerate(line_items, 1):
            qty = float(item.get("quantity") or 1.0)
            unit_price = float(item.get("unit_price") or 0.0)
            tax_rate = float(item.get("tax_rate") or 0.0)
            line_total = float(item.get("line_total") or (qty * unit_price))
            tax_amount = float(item.get("tax_amount") or (line_total * tax_rate / 100))

            line = InvoiceLine(
                id=uuid.uuid4(),
                invoice_id=invoice.id,
                line_number=i,
                description=item.get("description", f"Line {i}"),
                quantity=qty,
                unit_price=unit_price,
                tax_rate=tax_rate,
                tax_amount=tax_amount,
                line_total=line_total,
                item_code=item.get("item_code"),
            )
            invoice.lines.append(line)

    def _calculate_totals(self, invoice: Invoice):
        """Calculate totals from line items if not already set."""
        if invoice.subtotal > 0 and invoice.total_amount > 0:
            return  # Totals already set from extraction

        if not invoice.lines:
            return

        subtotal = sum(line.line_total for line in invoice.lines)
        tax_amount = sum(line.tax_amount for line in invoice.lines)

        if invoice.subtotal == 0:
            invoice.subtotal = subtotal
        if invoice.tax_amount == 0:
            invoice.tax_amount = tax_amount
        if invoice.total_amount == 0:
            invoice.total_amount = subtotal + tax_amount

    def _parse_date(self, date_str: str) -> datetime | None:
        """Parse various date formats."""
        from dateutil import parser as date_parser

        try:
            return date_parser.parse(date_str, dayfirst=False)
        except (ValueError, OverflowError):
            # Try common formats
            for fmt in ["%Y-%m-%d", "%d/%m/%Y", "%m/%d/%Y", "%d.%m.%Y", "%Y%m%d"]:
                try:
                    return datetime.strptime(date_str, fmt)
                except ValueError:
                    continue
        return None

    def _add_audit(
        self,
        db: AsyncSession,
        invoice: Invoice,
        action: str,
        details: dict,
    ):
        """Add an audit log entry."""
        audit = AuditLog(
            tenant_id=invoice.tenant_id,
            invoice_id=invoice.id,
            action=action,
            details=details,
            message=f"Extraction: {action}",
            timestamp=datetime.utcnow(),
        )
        db.add(audit)


# Singleton
pipeline = ExtractionPipeline()
