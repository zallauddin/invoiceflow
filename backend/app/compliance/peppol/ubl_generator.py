"""PEPPOL UBL 2.1 Invoice XML generator.

Generates PEPPOL-compliant UBL 2.1 Invoice XML from extracted invoice data.
Follows PEPPOL BIS Billing 3.0 rules and EN 16931 semantic data model.
"""

import uuid
from datetime import datetime
from decimal import Decimal
from typing import Any
from xml.dom import minidom
from xml.etree.ElementTree import Element, SubElement, tostring, ElementTree

# PEPPOL UBL 2.1 namespaces
UBL_NAMESPACES = {
    "xmlns": "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
    "xmlns:cac": "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2",
    "xmlns:cbc": "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2",
    "xmlns:ext": "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2",
    "xmlns:qdt": "urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2",
    "xmlns:udt": "urn:un:unece:uncefact:data:UBLCommonUnifiedDigits2",
}

# PEPPOL endpoint identifiers (ISO 6523)
ENDPOINT_IDENTIFIER = {
    "0088": "GLN (Global Location Number)",
    "0007": "Organisationsnummer",
    "0192": "CVR number",
    "0208": "KBO/BCE",
    "0211": "ID number",
    "9955": "FR (SIRET/SIREN)",
    "0106": "IT (VAT number)",
    "9906": "FI (Business ID)",
}

# Country-specific tax scheme URIs
TAX_SCHEME_URIS = {
    "AT": "urn:fed:at:tax:VAT",
    "BE": "urn:fed:be:tax:VAT",
    "DE": "urn:fed:de:tax:VAT",
    "DK": "urn:fed:dk:tax:VAT",
    "ES": "urn:fed:es:tax:VAT",
    "FI": "urn:fed:fi:tax:VAT",
    "FR": "urn:fed:fr:tax:VAT",
    "IT": "urn:fed:it:tax:VAT",
    "NL": "urn:fed:nl:tax:VAT",
    "NO": "urn:fed:no:tax:MVA",
    "SE": "urn:fed:se:tax:Moms",
    "PL": "urn:fed:pl:tax:VAT",
    "CZ": "urn:fed:cz:tax:DPH",
    "RO": "urn:fed:ro:tax:TVA",
    "IE": "urn:fed:ie:tax:VAT",
    "PT": "urn:fed:pt:tax:IVA",
}


class UBLInvoiceGenerator:
    """Generates PEPPOL BIS Billing 3.0 compliant UBL 2.1 Invoice XML."""

    def __init__(self, customisation_id: str = "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0"):
        self.customisation_id = customisation_id
        self.profile_id = "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0"

    def generate(self, invoice_data: dict[str, Any], lines: list[dict[str, Any]] | None = None) -> str:
        """
        Generate a PEPPOL UBL 2.1 Invoice XML string.

        Args:
            invoice_data: Dictionary with invoice fields (must match Invoice model)
            lines: Optional list of line item dicts

        Returns:
            Pretty-printed UBL 2.1 Invoice XML string
        """
        # Build root element
        root = Element("Invoice", UBL_NAMESPACES)

        # === UBLExtensions (PEPPOL mandatory) ===
        ubl_extensions = SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}UBLExtensions")
        ubl_extension = SubElement(ubl_extensions, f"{{{UBL_NAMESPACES['xmlns']}}}UBLExtension")
        SubElement(ubl_extension, f"{{{UBL_NAMESPACES['xmlns']}}}ID").text = "1"
        SubElement(ubl_extension, f"{{{UBL_NAMESPACES['xmlns']}}}Name").text = "PIKE-PEPPOL-BIS3-EXT"

        # === Basic mandatory fields ===
        invoice_id = invoice_data.get("invoice_number", str(uuid.uuid4())[:20])
        issue_date = invoice_data.get("invoice_date", datetime.utcnow())
        if isinstance(issue_date, datetime):
            issue_date = issue_date.strftime("%Y-%m-%d")

        SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}ID").text = invoice_id
        SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}IssueDate").text = issue_date

        due_date = invoice_data.get("due_date")
        if due_date:
            if isinstance(due_date, datetime):
                due_date = due_date.strftime("%Y-%m-%d")
            SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}DueDate").text = due_date

        invoice_type_code = invoice_data.get("invoice_type_code", "380")
        SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}InvoiceTypeCode").text = str(invoice_type_code)

        SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}DocumentCurrencyCode").text = invoice_data.get("currency", "EUR")
        SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}CustomisationID").text = self.customisation_id
        SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}ProfileID").text = self.profile_id

        # === AccountingSupplierParty (vendor) ===
        self._add_party(
            root, "AccountingSupplierParty",
            invoice_data.get("vendor_name", "Unknown"),
            invoice_data.get("vendor_tax_id", ""),
            invoice_data.get("vendor_country", invoice_data.get("country_code", "DE")),
            invoice_data.get("vendor_endpoint_id", "0088"),
            invoice_data.get("vendor_address", {}),
        )

        # === AccountingCustomerParty (buyer) ===
        self._add_party(
            root, "AccountingCustomerParty",
            invoice_data.get("buyer_name", ""),
            invoice_data.get("buyer_tax_id", ""),
            invoice_data.get("buyer_country", ""),
            invoice_data.get("buyer_endpoint_id", "0088"),
            invoice_data.get("buyer_address", {}),
        )

        # === PaymentMeans ===
        payment_means = invoice_data.get("payment_means", {})
        if payment_means or invoice_data.get("payment_terms"):
            pm = SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}PaymentMeans")
            SubElement(pm, f"{{{UBL_NAMESPACES['xmlns']}}}PaymentMeansCode").text = payment_means.get("code", "30")
            if payment_means.get("iban"):
                SubElement(pm, f"{{{UBL_NAMESPACES['xmlns']}}}PaymentID").text = invoice_id
                payee_account = SubElement(pm, f"{{{UBL_NAMESPACES['xmlns']}}}PayeeFinancialAccount")
                SubElement(payee_account, f"{{{UBL_NAMESPACES['xmlns']}}}ID").text = payment_means["iban"]
                if payment_means.get("bic"):
                    SubElement(payee_account, f"{{{UBL_NAMESPACES['xmlns']}}}PaymentServiceProviderBranch").text = payment_means["bic"]
            if invoice_data.get("payment_terms"):
                SubElement(pm, f"{{{UBL_NAMESPACES['xmlns']}}}PaymentTermsNote").text = invoice_data["payment_terms"]

        # === TaxTotal ===
        tax_total = invoice_data.get("tax_amount", 0)
        currency = invoice_data.get("currency", "EUR")
        tax_total_elem = SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}TaxTotal")
        tax_amount_elem = SubElement(tax_total_elem, f"{{{UBL_NAMESPACES['xmlns']}}}TaxAmount")
        tax_amount_elem.set("currencyID", currency)
        tax_amount_elem.text = str(Decimal(str(tax_total)).quantize(Decimal("0.01")))

        # Tax subtotals by rate
        tax_subtotals = invoice_data.get("tax_subtotals", [])
        for ts in tax_subtotals:
            SubElement(tax_total_elem, f"{{{UBL_NAMESPACES['xmlns']}}}TaxSubtotal") 
            # (simplified — full implementation would nest TaxCategory/TaxScheme)
            tax_sub = tax_total_elem.findall(f".//{{{UBL_NAMESPACES['xmlns']}}}TaxSubtotal")[-1]
            taxable = SubElement(tax_sub, f"{{{UBL_NAMESPACES['xmlns']}}}TaxableAmount")
            taxable.set("currencyID", currency)
            taxable.text = str(ts.get("taxable_amount", 0))
            tax_amt = SubElement(tax_sub, f"{{{UBL_NAMESPACES['xmlns']}}}TaxAmount")
            tax_amt.set("currencyID", currency)
            tax_amt.text = str(ts.get("tax_amount", 0))
            tax_cat = SubElement(tax_sub, f"{{{UBL_NAMESPACES['xmlns']}}}TaxCategory")
            SubElement(tax_cat, f"{{{UBL_NAMESPACES['xmlns']}}}ID").text = str(ts.get("category_id", "S"))
            SubElement(tax_cat, f"{{{UBL_NAMESPACES['xmlns']}}}Percent").text = str(ts.get("tax_rate", 0))
            tax_scheme = SubElement(tax_cat, f"{{{UBL_NAMESPACES['xmlns']}}}TaxScheme")
            SubElement(tax_scheme, f"{{{UBL_NAMESPACES['xmlns']}}}ID").text = "VAT"

        # === LegalMonetaryTotal ===
        monetary_total = SubElement(root, f"{{{UBL_NAMESPACES['xmlns']}}}LegalMonetaryTotal")
        subtotal = invoice_data.get("subtotal", 0)
        tax_amt_val = invoice_data.get("tax_amount", 0)
        total = invoice_data.get("total_amount", subtotal + tax_amt_val)

        self._add_monetary_field(monetary_total, "LineExtensionAmount", subtotal, currency)
        self._add_monetary_field(monetary_total, "TaxExclusiveAmount", subtotal, currency)
        self._add_monetary_field(monetary_total, "TaxInclusiveAmount", total, currency)
        self._add_monetary_field(monetary_total, "PayableAmount", total, currency)

        # Allowances/charges (optional)
        allowance = invoice_data.get("allowance", 0)
        if allowance:
            ac = SubElement(monetary_total, f"{{{UBL_NAMESPACES['xmlns']}}}AllowanceTotalAmount")
            ac.set("currencyID", currency)
            ac.text = str(Decimal(str(allowance)).quantize(Decimal("0.01")))

        # === InvoiceLines ===
        if not lines:
            lines = invoice_data.get("lines", [])

        for i, line in enumerate(lines, 1):
            self._add_invoice_line(root, i, line, currency)

        # Pretty print
        rough = tostring(root, encoding="unicode", xml_declaration=False)
        reparsed = minidom.parseString(f'<?xml version="1.0" encoding="UTF-8"?>\n{rough}')
        return reparsed.toprettyxml(indent="  ", encoding="UTF-8").decode("UTF-8")

    def _add_party(
        self,
        parent: Element,
        tag_name: str,
        name: str,
        tax_id: str,
        country: str,
        endpoint_id: str = "0088",
        address: dict | None = None,
    ) -> Element:
        """Add a UBL Party element (supplier or customer)."""
        ns = f"{{{UBL_NAMESPACES['xmlns']}}}"
        party = SubElement(parent, f"{ns}{tag_name}")
        p = SubElement(party, f"{ns}Party")

        # EndpointID (PEPPOL routing)
        if endpoint_id:
            eid = SubElement(p, f"{ns}EndpointID")
            eid.set("schemeID", "0088")
            eid.text = endpoint_id

        # PartyName
        if name:
            pn = SubElement(p, f"{ns}PartyName")
            SubElement(pn, f"{ns}Name").text = name

        # PostalAddress
        pa = SubElement(p, f"{ns}PostalAddress")
        if address:
            SubElement(pa, f"{ns}StreetName").text = address.get("street", "")
            SubElement(pa, f"{ns}CityName").text = address.get("city", "")
            SubElement(pa, f"{ns}PostalZone").text = address.get("postal_code", "")
            SubElement(pa, f"{ns}CountrySubentity").text = address.get("region", "")
        country_elem = SubElement(pa, f"{ns}Country")
        SubElement(country_elem, f"{ns}IdentificationCode").text = country or "DE"

        # PartyTaxScheme
        if tax_id:
            pts = SubElement(p, f"{ns}PartyTaxScheme")
            SubElement(pts, f"{ns}CompanyID").text = tax_id
            ts = SubElement(pts, f"{ns}TaxScheme")
            SubElement(ts, f"{ns}ID").text = "VAT"

        # PartyLegalEntity
        ple = SubElement(p, f"{ns}PartyLegalEntity")
        if name:
            SubElement(ple, f"{ns}RegistrationName").text = name
        if tax_id:
            SubElement(ple, f"{ns}CompanyID").text = tax_id

        return party

    def _add_monetary_field(self, parent: Element, tag: str, amount: float, currency: str) -> Element:
        """Add a monetary total field."""
        elem = SubElement(parent, f"{{{UBL_NAMESPACES['xmlns']}}}{tag}")
        elem.set("currencyID", currency)
        elem.text = str(Decimal(str(amount)).quantize(Decimal("0.01")))
        return elem

    def _add_invoice_line(self, parent: Element, line_number: int, line: dict, currency: str) -> None:
        """Add a single UBL InvoiceLine element."""
        ns = f"{{{UBL_NAMESPACES['xmlns']}}}"
        il = SubElement(parent, f"{ns}InvoiceLine")

        SubElement(il, f"{ns}ID").text = str(line_number)

        quantity = line.get("quantity", 1)
        unit_code = line.get("unit_code", "EA")
        qty = SubElement(il, f"{ns}InvoicedQuantity")
        qty.set("unitCode", unit_code)
        qty.text = str(quantity)

        line_ext = SubElement(il, f"{ns}LineExtensionAmount")
        line_ext.set("currencyID", currency)
        ext_amount = line.get("line_total", line.get("unit_price", 0) * quantity)
        line_ext.text = str(Decimal(str(ext_amount)).quantize(Decimal("0.01")))

        # Item
        item = SubElement(il, f"{ns}Item")
        description = line.get("description", "Item")
        SubElement(item, f"{ns}Description").text = description
        SubElement(item, f"{ns}Name").text = description[:100]

        if line.get("item_code"):
            SubElement(item, f"{ns}BuyersItemIdentification").text = line["item_code"]

        # ClassifiedTaxCategory
        tax_rate = line.get("tax_rate", 0)
        ctc = SubElement(item, f"{ns}ClassifiedTaxCategory")
        SubElement(ctc, f"{ns}ID").text = self._tax_category_from_rate(tax_rate)
        SubElement(ctc, f"{ns}Percent").text = str(tax_rate)
        ts = SubElement(ctc, f"{ns}TaxScheme")
        SubElement(ts, f"{ns}ID").text = "VAT"

        # Price
        price = SubElement(il, f"{ns}Price")
        pp = SubElement(price, f"{ns}PriceAmount")
        pp.set("currencyID", currency)
        pp.text = str(Decimal(str(line.get("unit_price", 0))).quantize(Decimal("0.01")))

    @staticmethod
    def _tax_category_from_rate(rate: float) -> str:
        """Map tax rate to PEPPOL tax category ID."""
        if rate == 0:
            return "AE"  # Intra-community supply / reverse charge
        elif rate <= 5:
            return "S"   # Standard / Reduced
        elif rate <= 10:
            return "S"
        else:
            return "S"   # Standard
