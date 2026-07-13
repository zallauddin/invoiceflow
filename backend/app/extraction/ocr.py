"""Tesseract OCR extraction with image preprocessing."""

import io
import logging
import re
from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional

import pytesseract
from PIL import Image, ImageFilter, ImageOps

logger = logging.getLogger(__name__)


@dataclass
class OCRResult:
    """Structured result from OCR extraction."""
    raw_text: str
    confidence: float
    invoice_number: Optional[str] = None
    invoice_date: Optional[str] = None
    due_date: Optional[str] = None
    vendor_name: Optional[str] = None
    vendor_tax_id: Optional[str] = None
    buyer_name: Optional[str] = None
    buyer_tax_id: Optional[str] = None
    currency: Optional[str] = None
    subtotal: Optional[float] = None
    tax_amount: Optional[float] = None
    total_amount: Optional[float] = None
    line_items: list[dict] = field(default_factory=list)
    metadata: dict = field(default_factory=dict)


class OCRExtractor:
    """Extract invoice data from images/PDFs using Tesseract OCR.

    Pipeline:
    1. Load image from bytes
    2. Preprocess (grayscale, denoise, deskew, binarize)
    3. Run Tesseract OCR
    4. Parse structured fields from raw text using regex patterns
    """

    # Common invoice field patterns
    PATTERNS = {
        "invoice_number": [
            r"(?:invoice|inv|bill)\s*(?:#|no|number|num|nr)\s*[:\s]*([A-Z0-9][\w\-/]{2,30})",
            r"(?:rechnung|faktura|fattura|factura)\s*(?:nr?|n[°o]|num[ée]ro)\s*[:\s]*([\w\-/]{2,30})",
        ],
        "date": [
            r"(?:date|datum|fecha|data)\s*[:\s]*(\d{1,2}[\s./\-]\w{3,9}[\s./\-]\d{2,4})",
            r"(?:date|datum|fecha|data)\s*[:\s]*(\d{1,2}[\s./\-]\d{1,2}[\s./\-]\d{2,4})",
            r"(\d{4}[\-/.]\d{2}[\-/.]\d{2})",
        ],
        "due_date": [
            r"(?:due\s*date|payment\s*due|fällig|vencimiento|scadenza)\s*[:\s]*(\d{1,2}[\s./\-]\w{3,9}[\s./\-]\d{2,4})",
            r"(?:due\s*date|payment\s*due|fällig|vencimiento|scadenza)\s*[:\s]*(\d{1,2}[\s./\-]\d{1,2}[\s./\-]\d{2,4})",
        ],
        "vendor": [
            r"(?:from|supplier|vendor|seller|lieferant|proveedor|fornitore)\s*[:\s]*(.{3,80})",
            r"(?:rechnungsteller|emitente|emisor)\s*[:\s]*(.{3,80})",
        ],
        "buyer": [
            r"(?:bill\s*to|customer|buyer|käufer|comprador|acquirente)\s*[:\s]*(.{3,80})",
            r"(?:rechnungsempfänger|destinatário|destinatario)\s*[:\s]*(.{3,80})",
        ],
        "tax_id": [
            r"(?:tax\s*id|vat\s*id|steuernr|uftid|cuit|cnpj|rfc|gst\s*no)\s*[:\s]*([\w\-\.]{5,20})",
            r"(?:ABN|ACN)\s*[:\s]*(\d[\d\s]{8,14})",
            r"(?:TIN|EIN)\s*[:\s]*(\d{2}\-?\d{7})",
        ],
        "currency": [
            r"(?:currency|währung|moneda|valuta)\s*[:\s]*([A-Z]{3})",
            r"([€£$])\s*[\d,\.]+",
            r"\b(USD|EUR|GBP|JPY|CAD|AUD|CHF|CNY|INR|BRL|MXN|SAR)\b",
        ],
        "total": [
            r"(?:total\s*amount|grand\s*total|gesamtbetrag|total\s*factura|importo\s*totale)\s*[:\s]*[\$€£]?\s*([\d,\.]+)",
            r"(?:total|summe|total|totale)\s*[:\s]*[\$€£]?\s*([\d,\.]+)",
            r"(?:amount\s*due|balance\s*due|saldo)\s*[:\s]*[\$€£]?\s*([\d,\.]+)",
        ],
        "subtotal": [
            r"(?:subtotal|sub\s*total|zwischensumme|base\s*imponible|imponibile)\s*[:\s]*[\$€£]?\s*([\d,\.]+)",
            r"(?:net\s*total|nettobetrag|neto)\s*[:\s]*[\$€£]?\s*([\d,\.]+)",
        ],
        "tax": [
            r"(?:vat\s*amount|tax\s*amount|mwst|iva|impuesto)\s*[:\s]*[\$€£]?\s*([\d,\.]+)",
            r"(?:tax\s*\d+\.?\d*%)\s*[:\s]*[\$€£]?\s*([\d,\.]+)",
        ],
    }

    CURRENCY_SYMBOLS = {"$": "USD", "€": "EUR", "£": "GBP", "¥": "JPY"}

    def preprocess_image(self, image_bytes: bytes) -> Image.Image:
        """Preprocess image for better OCR accuracy.

        Steps:
        1. Open image
        2. Convert to grayscale
        3. Increase contrast
        4. Apply median filter (denoise)
        5. Binarize (threshold)
        """
        img = Image.open(io.BytesIO(image_bytes))

        # Convert to grayscale
        img = ImageOps.grayscale(img)

        # Increase contrast via autocontrast
        img = ImageOps.autocontrast(img, cutoff=2)

        # Denoise with median filter
        img = img.filter(ImageFilter.MedianFilter(size=3))

        # Binarize with adaptive-like threshold
        threshold = 140
        img = img.point(lambda x: 255 if x > threshold else 0, "1")

        # Scale up small images for better OCR
        width, height = img.size
        if width < 1000:
            scale = 1000 / width
            new_size = (int(width * scale), int(height * scale))
            img = img.resize(new_size, Image.LANCZOS)

        return img

    def extract_text(self, image_bytes: bytes) -> tuple[str, float]:
        """Run Tesseract OCR and return (text, confidence)."""
        img = self.preprocess_image(image_bytes)

        # Get detailed OCR data for confidence
        data = pytesseract.image_to_data(img, output_type=pytesseract.Output.DICT)

        # Calculate average confidence (excluding -1 = failed)
        confs = [int(c) for c in data["conf"] if int(c) > 0]
        avg_confidence = sum(confs) / len(confs) / 100.0 if confs else 0.0

        # Get full text
        text = pytesseract.image_to_string(img, lang="eng+deu+fra+spa+ita")

        return text, min(avg_confidence, 1.0)

    def parse_fields(self, text: str) -> dict:
        """Extract structured fields from OCR text using regex patterns."""
        result = {}

        # Invoice number
        for pattern in self.PATTERNS["invoice_number"]:
            match = re.search(pattern, text, re.IGNORECASE)
            if match:
                result["invoice_number"] = match.group(1).strip()
                break

        # Dates
        for pattern in self.PATTERNS["date"]:
            match = re.search(pattern, text, re.IGNORECASE)
            if match:
                result["invoice_date"] = match.group(1).strip()
                break

        for pattern in self.PATTERNS["due_date"]:
            match = re.search(pattern, text, re.IGNORECASE)
            if match:
                result["due_date"] = match.group(1).strip()
                break

        # Vendor
        for pattern in self.PATTERNS["vendor"]:
            match = re.search(pattern, text, re.IGNORECASE)
            if match:
                result["vendor_name"] = match.group(1).strip()
                break

        # Buyer
        for pattern in self.PATTERNS["buyer"]:
            match = re.search(pattern, text, re.IGNORECASE)
            if match:
                result["buyer_name"] = match.group(1).strip()
                break

        # Tax IDs
        tax_ids = []
        for pattern in self.PATTERNS["tax_id"]:
            matches = re.findall(pattern, text, re.IGNORECASE)
            tax_ids.extend(m.strip() for m in matches)
        if tax_ids:
            result["tax_ids"] = tax_ids

        # Currency
        for pattern in self.PATTERNS["currency"]:
            match = re.search(pattern, text, re.IGNORECASE)
            if match:
                currency = match.group(1)
                if currency in self.CURRENCY_SYMBOLS:
                    currency = self.CURRENCY_SYMBOLS[currency]
                result["currency"] = currency
                break

        # Financial amounts
        for field_name, patterns in [
            ("total", self.PATTERNS["total"]),
            ("subtotal", self.PATTERNS["subtotal"]),
            ("tax", self.PATTERNS["tax"]),
        ]:
            for pattern in patterns:
                match = re.search(pattern, text, re.IGNORECASE)
                if match:
                    try:
                        amount_str = match.group(1).replace(",", "").replace(" ", "")
                        result[field_name] = float(amount_str)
                    except ValueError:
                        pass
                    break

        return result

    def extract(self, file_bytes: bytes, mime_type: str = "image/png") -> OCRResult:
        """Full OCR extraction pipeline.

        Args:
            file_bytes: Raw file bytes (image or PDF)
            mime_type: MIME type of the file

        Returns:
            OCRResult with raw text, confidence, and parsed fields
        """
        # For PDFs, extract first page as image
        if "pdf" in mime_type.lower():
            file_bytes = self._pdf_to_image(file_bytes)

        text, confidence = self.extract_text(file_bytes)
        fields = self.parse_fields(text)

        logger.info(
            f"OCR extraction complete: confidence={confidence:.2f}, "
            f"fields_found={list(fields.keys())}"
        )

        return OCRResult(
            raw_text=text,
            confidence=confidence,
            **fields,
        )

    def _pdf_to_image(self, pdf_bytes: bytes) -> bytes:
        """Convert first page of PDF to image bytes."""
        import fitz  # PyMuPDF

        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        if doc.page_count == 0:
            raise ValueError("PDF has no pages")

        page = doc[0]
        # Render at 2x resolution for better OCR
        mat = fitz.Matrix(2.0, 0, 0, 2.0, 0, 0)
        pix = page.get_pixmap(matrix=mat)
        img = Image.open(io.BytesIO(pix.tobytes("png")))
        doc.close()

        buf = io.BytesIO()
        img.save(buf, format="PNG")
        return buf.getvalue()
