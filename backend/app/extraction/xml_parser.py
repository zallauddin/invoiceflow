"""XML invoice parsing — ZUGFeRD/Factur-X, UBL 2.1, CII."""

import io
import logging
from dataclasses import dataclass, field
from typing import Optional

import fitz  # PyMuPDF
from lxml import etree

logger = logging.getLogger(__name__)

# ZUGFeRD/Factur-X embedded XML attachment filename patterns
FACTUR_X_ATTACHMENT_NAMES = [
    "factur-x.xml",
    "zugferd-invoice.xml",
    "xrechnung.xml",
    "invoice.xml",
]

# UBL 2.1 namespace
UBL_NS = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
UBL_NS_PREFIX = {"ubl": UBL_NS}

# CII namespace
CII_NS = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
CII_NS_PREFIX = {"cii": CII_NS}

# ZUGFeRD/Factur-X namespace
FACTURX_NS = "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0"
FACTURX_NS_PREFIX = {"fx": FACTURX_NS}


@dataclass
class XMLExtractionResult:
    """Result from XML invoice parsing."""
    format: str  # "factur-x", "ubl", "cii"
    raw_xml: str
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
    payment_terms: Optional[str] = None
    metadata: dict = field(default_factory=dict)
    success: bool = True
    error: Optional[str] = None


class XMLInvoiceParser:
    """Parse structured XML invoices from multiple formats.

    Supports:
    - ZUGFeRD/Factur-X (embedded in PDF as attachment)
    - UBL 2.1 XML
    - UN/CEFACT CII XML
    """

    def parse_pdf(self, pdf_bytes: bytes) -> Optional[XMLExtractionResult]:
        """Extract and parse XML from PDF (ZUGFeRD/Factur-X)."""
        try:
            doc = fitz.open(stream=pdf_bytes, filetype="pdf")

            # Search for embedded XML attachments
            for name in FACTUR_X_ATTACHMENT_NAMES:
                xml_bytes = self._extract_attachment(doc, name)
                if xml_bytes:
                    doc.close()
                    result = self._detect_and_parse(xml_bytes)
                    if result and result.success:
                        result.metadata["source_pdf"] = True
                        result.metadata["attachment_name"] = name
                        return result

            # No Factur-X attachment found — try to extract XML from page 1
            # (some Factur-X embeds XML as visible content)
            xml_bytes = self._extract_xml_from_page(doc, 0)
            doc.close()

            if xml_bytes:
                result = self._detect_and_parse(xml_bytes)
                if result and result.success:
                    result.metadata["source_pdf"] = True
                    result.metadata["extraction_method"] = "page_content"
                    return result

            return None

        except Exception as e:
            logger.error(f"PDF XML extraction failed: {e}")
            return XMLExtractionResult(
                format="unknown",
                raw_xml="",
                success=False,
                error=str(e),
            )

    def parse_xml(self, xml_bytes: bytes) -> Optional[XMLExtractionResult]:
        """Parse raw XML bytes."""
        return self._detect_and_parse(xml_bytes)

    def parse_xml_string(self, xml_string: str) -> Optional[XMLExtractionResult]:
        """Parse XML string."""
        return self._detect_and_parse(xml_string.encode("utf-8"))

    def _detect_and_parse(self, xml_bytes: bytes) -> Optional[XMLExtractionResult]:
        """Detect XML format and dispatch to appropriate parser."""
        try:
            root = etree.fromstring(xml_bytes)
        except etree.XMLSyntaxError as e:
            logger.warning(f"Invalid XML: {e}")
            return XMLExtractionResult(
                format="unknown",
                raw_xml=xml_bytes.decode("utf-8", errors="replace"),
                success=False,
                error=f"Invalid XML: {e}",
            )

        raw_xml = etree.tostring(root, pretty_print=True).decode("utf-8")

        # Detect format
        tag = root.tag.lower()
        ns = root.nsmap.get(None, "")

        if "crossindustryinvoice" in tag or "rsm" in tag.lower():
            return self._parse_cii(root, raw_xml)
        elif "invoice" in tag and UBL_NS in ns:
            return self._parse_ubl(root, raw_xml)
        elif "invoice" in tag and (
            "factur-x" in tag or "zugferd" in tag or "en16931" in ns
        ):
            return self._parse_facturx(root, raw_xml)
        else:
            # Try to detect from child elements
            children = [c.tag.lower() for c in root]
            if any("headerexchangedocument" in c for c in children):
                return self._parse_cii(root, raw_xml)
            elif any("invoice" in c for c in children):
                return self._parse_ubl(root, raw_xml)
            else:
                # Default: try Factur-X
                return self._parse_facturx(root, raw_xml)

    def _parse_facturx(self, root: etree._Element, raw_xml: str) -> XMLExtractionResult:
        """Parse ZUGFeRD/Factur-X XML (EN 16931 compliant)."""
        ns = root.nsmap.get(None, "")
        prefix = f"{{{ns}}}" if ns else ""

        def find_text(path: str) -> Optional[str]:
            try:
                el = root.find(f".//{prefix}{path}")
                return el.text.strip() if el is not None and el.text else None
            except Exception:
                return None

        def find_all_text(path: str) -> list[str]:
            try:
                return [
                    el.text.strip()
                    for el in root.findall(f".//{prefix}{path}")
                    if el.text
                ]
            except Exception:
                return []

        # Extract header fields
        result = XMLExtractionResult(
            format="factur-x",
            raw_xml=raw_xml,
            invoice_number=find_text("ID"),
            invoice_date=find_text("IssueDate"),
            due_date=find_text("DueDate"),
            currency=find_text("DocumentCurrencyCode"),
            payment_terms=find_text("PaymentTerms/Note"),
        )

        # Seller (vendor)
        result.vendor_name = find_text("AccountingSupplierParty/Party/Name")
        result.vendor_tax_id = find_text(
            "AccountingSupplierParty/Party/PartyTaxScheme/CompanyID"
        )

        # Buyer
        result.buyer_name = find_text("AccountingCustomerParty/Party/Name")
        result.buyer_tax_id = find_text(
            "AccountingCustomerParty/Party/PartyTaxScheme/CompanyID"
        )

        # Totals
        result.total_amount = self._parse_float(
            find_text("LegalMonetaryTotal/TaxExclusiveAmount")
        )
        result.tax_amount = self._parse_float(
            find_text("LegalMonetaryTotal/TaxAmount")
        )
        result.subtotal = self._parse_float(
            find_text("LegalMonetaryTotal/LineExtensionAmount")
        )

        if result.total_amount is None and result.subtotal is not None and result.tax_amount is not None:
            result.total_amount = result.subtotal + result.tax_amount

        # Line items
        for item in root.findall(f".//{prefix}InvoiceLine"):
            line = {
                "line_number": self._parse_int(find_text_in(item, prefix, "ID")),
                "description": find_text_in(item, prefix, "Item/Name"),
                "quantity": self._parse_float(find_text_in(item, prefix, "InvoicedQuantity")),
                "unit_price": self._parse_float(find_text_in(item, prefix, "Price/PriceAmount")),
                "tax_rate": self._parse_float(
                    find_text_in(item, prefix, "Item/ClassifiedTaxCategory/Percent")
                ),
                "line_total": self._parse_float(
                    find_text_in(item, prefix, "LineExtensionAmount")
                ),
            }
            if line.get("description"):
                result.line_items.append(line)

        return result

    def _parse_ubl(self, root: etree._Element, raw_xml: str) -> XMLExtractionResult:
        """Parse UBL 2.1 XML invoice."""
        # UBL uses namespace prefix
        def find_text(path: str) -> Optional[str]:
            try:
                el = root.find(f".//ubl:{path}", UBL_NS_PREFIX)
                if el is None:
                    # Try without prefix
                    el = root.find(f".//{path}")
                return el.text.strip() if el is not None and el.text else None
            except Exception:
                return None

        result = XMLExtractionResult(
            format="ubl",
            raw_xml=raw_xml,
            invoice_number=find_text("ID"),
            invoice_date=find_text("IssueDate"),
            due_date=find_text("DueDate"),
            currency=find_text("DocumentCurrencyCode"),
            payment_terms=find_text("PaymentTerms/Note"),
        )

        # Supplier (vendor)
        result.vendor_name = find_text("AccountingSupplierParty/Party/PartyName/Name")
        result.vendor_tax_id = find_text(
            "AccountingSupplierParty/Party/PartyTaxScheme/CompanyID"
        )

        # Customer (buyer)
        result.buyer_name = find_text("AccountingCustomerParty/Party/PartyName/Name")
        result.buyer_tax_id = find_text(
            "AccountingCustomerParty/Party/PartyTaxScheme/CompanyID"
        )

        # Totals
        result.total_amount = self._parse_float(
            find_text("LegalMonetaryTotal/TaxExclusiveAmount")
        )
        result.tax_amount = self._parse_float(
            find_text("LegalMonetaryTotal/TaxAmount")
        )
        result.subtotal = self._parse_float(
            find_text("LegalMonetaryTotal/LineExtensionAmount")
        )

        if result.total_amount is None and result.subtotal is not None and result.tax_amount is not None:
            result.total_amount = result.subtotal + result.tax_amount

        # Line items
        for item in root.findall(".//ubl:InvoiceLine", UBL_NS_PREFIX):
            line = {
                "line_number": self._parse_int(find_text_in_ns(item, UBL_NS_PREFIX, "ID")),
                "description": find_text_in_ns(item, UBL_NS_PREFIX, "Item/Description"),
                "quantity": self._parse_float(
                    find_text_in_ns(item, UBL_NS_PREFIX, "InvoicedQuantity")
                ),
                "unit_price": self._parse_float(
                    find_text_in_ns(item, UBL_NS_PREFIX, "Price/PriceAmount")
                ),
                "tax_rate": self._parse_float(
                    find_text_in_ns(
                        item, UBL_NS_PREFIX, "Item/ClassifiedTaxCategory/Percent"
                    )
                ),
                "line_total": self._parse_float(
                    find_text_in_ns(item, UBL_NS_PREFIX, "LineExtensionAmount")
                ),
            }
            if line.get("description"):
                result.line_items.append(line)

        return result

    def _parse_cii(self, root: etree._Element, raw_xml: str) -> XMLExtractionResult:
        """Parse UN/CEFACT CII (Cross-Industry Invoice) XML."""
        def find_text(path: str) -> Optional[str]:
            try:
                el = root.find(f".//{path}", CII_NS_PREFIX)
                if el is None:
                    # Try with any namespace
                    local = path.split("/")[-1]
                    el = root.find(f".//*[local-name()='{local}']")
                return el.text.strip() if el is not None and el.text else None
            except Exception:
                return None

        # CII structure is different from UBL
        result = XMLExtractionResult(
            format="cii",
            raw_xml=raw_xml,
            invoice_number=find_text(
                "HeaderExchangedDocument/ID"
            ),
            invoice_date=find_text(
                "HeaderExchangedDocument/IssueDateTime/DateTimeString"
            ),
        )

        # ExchangedParty (roles)
        supply_chain_trade_agreement = root.find(
            ".//SupplyChainTradeAgreement", CII_NS_PREFIX
        )
        if supply_chain_trade_agreement is not None:
            # Seller
            seller = supply_chain_trade_agreement.find(
                ".//SellerTradeParty", CII_NS_PREFIX
            )
            if seller is not None:
                name_el = seller.find(".//Name", CII_NS_PREFIX)
                result.vendor_name = name_el.text if name_el is not None and name_el.text else None
                tax_el = seller.find(".//ID", CII_NS_PREFIX)
                result.vendor_tax_id = tax_el.text if tax_el is not None and tax_el.text else None

            # Buyer
            buyer = supply_chain_trade_agreement.find(
                ".//BuyerTradeParty", CII_NS_PREFIX
            )
            if buyer is not None:
                name_el = buyer.find(".//Name", CII_NS_PREFIX)
                result.buyer_name = name_el.text if name_el is not None and name_el.text else None
                tax_el = buyer.find(".//ID", CII_NS_PREFIX)
                result.buyer_tax_id = tax_el.text if tax_el is not None and tax_el.text else None

        # Financial totals
        supply_chain_trade_settlement = root.find(
            ".//SupplyChainTradeSettlement", CII_NS_PREFIX
        )
        if supply_chain_trade_settlement is not None:
            # Currency
            currency_el = supply_chain_trade_settlement.find(
                ".//InvoiceCurrencyCode", CII_NS_PREFIX
            )
            if currency_el is not None and currency_el.text:
                result.currency = currency_el.text

            # Totals
            specified_trade_settlement_header_monetary_summation = (
                supply_chain_trade_settlement.find(
                    ".//SpecifiedTradeSettlementHeaderMonetarySummation",
                    CII_NS_PREFIX,
                )
            )
            if specified_trade_settlement_header_monetary_summation is not None:
                for amount_el in specified_trade_settlement_header_monetary_summation:
                    type_code_el = amount_el.find(".//TypeCode", CII_NS_PREFIX)
                    if type_code_el is not None and type_code_el.text:
                        type_code = type_code_el.text
                        value = self._parse_float(amount_el.text)
                        if type_code == "360":  # Tax basis total
                            result.subtotal = value
                        elif type_code == "381":  # Tax total
                            result.tax_amount = value
                        elif type_code == "393":  # Grand total
                            result.total_amount = value

        return result

    def _extract_attachment(self, doc: fitz.Document, name: str) -> Optional[bytes]:
        """Extract a named attachment from a PDF."""
        try:
            for i in range(doc.emb_count):
                emb = doc.emb_get(i)
                if emb and emb.get("name", "").lower() == name.lower():
                    return emb.get("content")
        except Exception:
            pass
        return None

    def _extract_xml_from_page(
        self, doc: fitz.Document, page_num: int
    ) -> Optional[bytes]:
        """Try to extract XML content from a PDF page's text."""
        if page_num >= doc.page_count:
            return None

        page = doc[page_num]
        text = page.get_text()

        # Look for XML content
        xml_start = text.find("<?xml")
        if xml_start < 0:
            xml_start = text.find("<")
        if xml_start < 0:
            return None

        xml_text = text[xml_start:]

        # Find end of XML
        xml_end = xml_text.rfind(">")
        if xml_end < 0:
            return None

        xml_text = xml_text[: xml_end + 1]

        try:
            # Validate it's actually XML
            etree.fromstring(xml_text.encode("utf-8"))
            return xml_text.encode("utf-8")
        except etree.XMLSyntaxError:
            return None


def find_text_in(
    element: etree._Element, prefix: str, path: str
) -> Optional[str]:
    """Find text in an XML element with namespace prefix."""
    try:
        el = element.find(f"{prefix}{path}")
        return el.text.strip() if el is not None and el.text else None
    except Exception:
        return None


def find_text_in_ns(
    element: etree._Element, ns_map: dict, path: str
) -> Optional[str]:
    """Find text in an XML element with namespace map."""
    try:
        # Try with UBL namespace
        el = element.find(f"ubl:{path}", ns_map)
        if el is None:
            # Try local name
            local = path.split("/")[-1]
            el = element.find(f".//*[local-name()='{local}']")
        return el.text.strip() if el is not None and el.text else None
    except Exception:
        return None
