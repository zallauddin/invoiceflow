"""PEPPOL Invoice XML validation.

Validates generated UBL 2.1 Invoice XML against PEPPOL BIS Billing 3.0 rules.
Includes structural validation and PEPPOL-specific business rules.
"""

from dataclasses import dataclass, field
from typing import Any
from xml.etree.ElementTree import fromstring


@dataclass
class ValidationResult:
    """Result of PEPPOL validation."""
    valid: bool = True
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    rules_checked: int = 0
    rules_passed: int = 0


class PEPPOLValidator:
    """Validates UBL 2.1 Invoice XML against PEPPOL BIS Billing 3.0 rules."""

    # Required fields per PEPPOL BIS Billing 3.0
    REQUIRED_ROOT_FIELDS = [
        "ID", "IssueDate", "InvoiceTypeCode", "DocumentCurrencyCode",
        "AccountingSupplierParty", "AccountingCustomerParty",
        "LegalMonetaryTotal",
    ]

    REQUIRED_PARTY_FIELDS = [
        "EndpointID", "PostalAddress", "PartyLegalEntity",
    ]

    REQUIRED_MONETARY_FIELDS = [
        "LineExtensionAmount", "TaxExclusiveAmount",
        "TaxInclusiveAmount", "PayableAmount",
    ]

    def validate_xml(self, xml_string: str) -> ValidationResult:
        """
        Validate a UBL 2.1 Invoice XML string.

        Args:
            xml_string: The UBL 2.1 Invoice XML to validate

        Returns:
            ValidationResult with errors and warnings
        """
        result = ValidationResult()

        try:
            root = fromstring(xml_string)
        except Exception as e:
            result.valid = False
            result.errors.append(f"XML parse error: {str(e)}")
            return result

        # Check root element
        self._check_root_element(root, result)

        # Check required fields
        self._check_required_fields(root, result)

        # Check party details
        self._check_party("AccountingSupplierParty", root, result, "Supplier")
        self._check_party("AccountingCustomerParty", root, result, "Customer")

        # Check monetary totals
        self._check_monetary_totals(root, result)

        # Check invoice lines
        self._check_invoice_lines(root, result)

        # Check tax totals
        self._check_tax_totals(root, result)

        # Business rules
        self._check_business_rules(root, result)

        # Calculate pass rate
        result.rules_checked = len(result.errors) + len(result.warnings) + result.rules_passed
        result.valid = len(result.errors) == 0

        return result

    def _check_root_element(self, root: Element, result: ValidationResult) -> None:
        """Validate root element name."""
        result.rules_checked += 1
        tag = root.tag.split("}")[-1] if "}" in root.tag else root.tag
        if tag != "Invoice":
            result.errors.append(f"Root element must be 'Invoice', got '{tag}'")
        else:
            result.rules_passed += 1

    def _check_required_fields(self, root: Element, result: ValidationResult) -> None:
        """Check all PEPPOL required fields exist."""
        ns = self._get_namespace(root)
        for field_name in self.REQUIRED_ROOT_FIELDS:
            result.rules_checked += 1
            elem = root.find(f"{{{ns}}}{field_name}") if ns else root.find(field_name)
            if elem is None:
                result.errors.append(f"Missing required field: {field_name}")
            elif not elem.text and len(elem) == 0:
                result.errors.append(f"Empty required field: {field_name}")
            else:
                result.rules_passed += 1

    def _check_party(self, tag: str, root: Element, result: ValidationResult, role: str) -> None:
        """Validate party (supplier/customer) structure."""
        ns = self._get_namespace(root)
        party_elem = root.find(f"{{{ns}}}{tag}") if ns else root.find(tag)
        result.rules_checked += 1

        if party_elem is None:
            result.errors.append(f"Missing {role} party element: {tag}")
            return

        result.rules_passed += 1

        # Check required party fields
        party = party_elem.find(f"{{{ns}}}Party") if ns else party_elem.find("Party")
        if party is None:
            result.errors.append(f"{role}: Missing Party element inside {tag}")
            return

        for field_name in self.REQUIRED_PARTY_FIELDS:
            result.rules_checked += 1
            field_elem = party.find(f"{{{ns}}}{field_name}") if ns else party.find(field_name)
            if field_elem is None:
                result.errors.append(f"{role}: Missing required field: {field_name}")
            else:
                result.rules_passed += 1

        # Validate EndpointID schemeID
        endpoint = party.find(f"{{{ns}}}EndpointID") if ns else party.find("EndpointID")
        if endpoint is not None:
            result.rules_checked += 1
            scheme = endpoint.get("schemeID")
            if not scheme:
                result.errors.append(f"{role}: EndpointID missing schemeID attribute")
            elif scheme not in ("0088", "0007", "0192", "0208", "0211", "9955", "0106", "9906", "0184", "0186", "0188", "0190", "0196", "0198", "0200", "0204"):
                result.warnings.append(f"{role}: EndpointID scheme '{scheme}' may not be supported by all PEPPOL participants")
            else:
                result.rules_passed += 1

    def _check_monetary_totals(self, root: Element, result: ValidationResult) -> None:
        """Validate LegalMonetaryTotal structure."""
        ns = self._get_namespace(root)
        lmt = root.find(f"{{{ns}}}LegalMonetaryTotal") if ns else root.find("LegalMonetaryTotal")

        result.rules_checked += 1
        if lmt is None:
            result.errors.append("Missing LegalMonetaryTotal element")
            return
        result.rules_passed += 1

        for field_name in self.REQUIRED_MONETARY_FIELDS:
            result.rules_checked += 1
            elem = lmt.find(f"{{{ns}}}{field_name}") if ns else lmt.find(field_name)
            if elem is None:
                result.errors.append(f"LegalMonetaryTotal: Missing {field_name}")
            elif not elem.text:
                result.errors.append(f"LegalMonetaryTotal: Empty {field_name}")
            else:
                # Validate currencyID attribute
                currency = elem.get("currencyID")
                if not currency:
                    result.errors.append(f"LegalMonetaryTotal.{field_name}: Missing currencyID attribute")
                elif len(currency) != 3:
                    result.errors.append(f"LegalMonetaryTotal.{field_name}: Invalid currency code '{currency}'")
                else:
                    result.rules_passed += 1

    def _check_invoice_lines(self, root: Element, result: ValidationResult) -> None:
        """Validate invoice lines."""
        ns = self._get_namespace(root)
        lines = root.findall(f"{{{ns}}}InvoiceLine") if ns else root.findall("InvoiceLine")

        result.rules_checked += 1
        if not lines:
            result.warnings.append("No invoice lines found (PEPPOL recommends at least one)")
            return
        result.rules_passed += 1

        for i, line in enumerate(lines, 1):
            # Each line needs ID, InvoicedQuantity, LineExtensionAmount, Item, Price
            for req in ["ID", "InvoicedQuantity", "LineExtensionAmount", "Item", "Price"]:
                result.rules_checked += 1
                elem = line.find(f"{{{ns}}}{req}") if ns else line.find(req)
                if elem is None:
                    result.errors.append(f"Line {i}: Missing required field {req}")
                else:
                    result.rules_passed += 1

            # Validate line has Item/Name
            item = line.find(f"{{{ns}}}Item") if ns else line.find("Item")
            if item is not None:
                result.rules_checked += 1
                name = item.find(f"{{{ns}}}Name") if ns else item.find("Name")
                if name is None or not name.text:
                    result.errors.append(f"Line {i}: Item missing Name")
                else:
                    result.rules_passed += 1

    def _check_tax_totals(self, root: Element, result: ValidationResult) -> None:
        """Validate TaxTotal element."""
        ns = self._get_namespace(root)
        tax_totals = root.findall(f"{{{ns}}}TaxTotal") if ns else root.findall("TaxTotal")

        result.rules_checked += 1
        if not tax_totals:
            result.errors.append("Missing TaxTotal element")
            return
        result.rules_passed += 1

        # First TaxTotal must have TaxAmount
        tt = tax_totals[0]
        result.rules_checked += 1
        ta = tt.find(f"{{{ns}}}TaxAmount") if ns else tt.find("TaxAmount")
        if ta is None:
            result.errors.append("TaxTotal: Missing TaxAmount")
        elif not ta.get("currencyID"):
            result.errors.append("TaxTotal.TaxAmount: Missing currencyID")
        else:
            result.rules_passed += 1

    def _check_business_rules(self, root: Element, result: ValidationResult) -> None:
        """Check PEPPOL business rules."""
        ns = self._get_namespace(root)

        # BR-1: Invoice number must not be empty
        result.rules_checked += 1
        invoice_id = root.find(f"{{{ns}}}ID") if ns else root.find("ID")
        if invoice_id is not None and invoice_id.text:
            result.rules_passed += 1
        else:
            result.errors.append("BR-1: Invoice ID must not be empty")

        # BR-2: IssueDate must be valid date
        result.rules_checked += 1
        issue_date = root.find(f"{{{ns}}}IssueDate") if ns else root.find("IssueDate")
        if issue_date is not None and issue_date.text:
            try:
                from datetime import datetime
                datetime.strptime(issue_date.text, "%Y-%m-%d")
                result.rules_passed += 1
            except ValueError:
                result.errors.append("BR-2: IssueDate must be in YYYY-MM-DD format")
        else:
            result.errors.append("BR-2: IssueDate is required")

        # BR-3: InvoiceTypeCode must be valid
        result.rules_checked += 1
        itc = root.find(f"{{{ns}}}InvoiceTypeCode") if ns else root.find("InvoiceTypeCode")
        if itc is not None and itc.text:
            valid_codes = ["380", "381", "383", "388", "394", "395", "527", "825"]
            if itc.text in valid_codes:
                result.rules_passed += 1
            else:
                result.warnings.append(f"BR-3: InvoiceTypeCode '{itc.text}' is not in standard list (PEPPOL allows it)")
                result.rules_passed += 1  # Warning, not error
        else:
            result.errors.append("BR-3: InvoiceTypeCode is required")

        # BR-13: LineExtensionAmount must equal sum of lines
        result.rules_checked += 1
        lmt = root.find(f"{{{ns}}}LegalMonetaryTotal") if ns else root.find("LegalMonetaryTotal")
        if lmt is not None:
            lea = lmt.find(f"{{{ns}}}LineExtensionAmount") if ns else lmt.find("LineExtensionAmount")
            if lea is not None and lea.text:
                try:
                    declared = float(lea.text)
                    lines = root.findall(f"{{{ns}}}InvoiceLine") if ns else root.findall("InvoiceLine")
                    computed = 0.0
                    for line in lines:
                        ext = line.find(f"{{{ns}}}LineExtensionAmount") if ns else line.find("LineExtensionAmount")
                        if ext is not None and ext.text:
                            computed += float(ext.text)
                    if abs(declared - computed) < 0.02:
                        result.rules_passed += 1
                    else:
                        result.errors.append(f"BR-13: LineExtensionAmount mismatch: declared={declared}, computed={computed}")
                except (ValueError, TypeError):
                    result.warnings.append("BR-13: Could not validate LineExtensionAmount")
                    result.rules_passed += 1
            else:
                result.rules_passed += 1  # Skip if not present
        else:
            result.rules_passed += 1

    @staticmethod
    def _get_namespace(root: Element) -> str:
        """Extract namespace from root element."""
        tag = root.tag
        if tag.startswith("{"):
            return tag.split("}")[0][1:]
        return ""


# Re-import needed for type hints
from xml.etree.ElementTree import Element
