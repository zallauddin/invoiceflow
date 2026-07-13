using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 CreditNote root document — maps to the &lt;CreditNote&gt; XML root element.
/// Similar structure to Invoice but with CreditNote-specific elements.</summary>
[XmlRoot("CreditNote", Namespace = UblNamespaces.CreditNote)]
public class UblCreditNote
{
    // --- CBC (Common Basic Components) ---

    /// <summary>UBL version identifier. Fixed to "2.1".</summary>
    [XmlElement("UBLVersionID", Namespace = UblNamespaces.Cbc)]
    public string? UblVersionId { get; set; } = "2.1";

    /// <summary>Customization identifier (e.g., EN 16931).</summary>
    [XmlElement("CustomizationID", Namespace = UblNamespaces.Cbc)]
    public string? CustomizationId { get; set; }

    /// <summary>Profile identifier (e.g., Peppol Billing).</summary>
    [XmlElement("ProfileID", Namespace = UblNamespaces.Cbc)]
    public string? ProfileId { get; set; }

    /// <summary>BT-1: Credit note number (mandatory).</summary>
    [XmlElement("ID", Namespace = UblNamespaces.Cbc)]
    public string? Id { get; set; }

    /// <summary>BT-2: Credit note issue date (mandatory).</summary>
    [XmlElement("IssueDate", Namespace = UblNamespaces.Cbc)]
    public DateTime? IssueDate { get; set; }

    /// <summary>BT-9: Credit note due date.</summary>
    [XmlElement("DueDate", Namespace = UblNamespaces.Cbc)]
    public DateTime? DueDate { get; set; }

    /// <summary>BT-3: Credit note type code (381 for credit note).</summary>
    [XmlElement("CreditNoteTypeCode", Namespace = UblNamespaces.Cbc)]
    public string? CreditNoteTypeCode { get; set; }

    /// <summary>BT-5: Document currency code (mandatory, ISO 4217).</summary>
    [XmlElement("DocumentCurrencyCode", Namespace = UblNamespaces.Cbc)]
    public string? DocumentCurrencyCode { get; set; }

    /// <summary>BT-6: Tax currency code (optional).</summary>
    [XmlElement("TaxCurrencyCode", Namespace = UblNamespaces.Cbc)]
    public string? TaxCurrencyCode { get; set; }

    /// <summary>BT-11: Buyer reference.</summary>
    [XmlElement("BuyerReference", Namespace = UblNamespaces.Cbc)]
    public string? BuyerReference { get; set; }

    // --- CAC (Common Aggregate Components) ---

    /// <summary>BT-27: AccountingSupplierParty (seller/vendor).</summary>
    [XmlElement("AccountingSupplierParty", Namespace = UblNamespaces.Cac)]
    public UblSupplierParty? AccountingSupplierParty { get; set; }

    /// <summary>BT-44: AccountingCustomerParty (buyer).</summary>
    [XmlElement("AccountingCustomerParty", Namespace = UblNamespaces.Cac)]
    public UblCustomerParty? AccountingCustomerParty { get; set; }

    /// <summary>BT-110+: TaxTotal (one per tax type).</summary>
    [XmlElement("TaxTotal", Namespace = UblNamespaces.Cac)]
    public List<UblTaxTotal> TaxTotals { get; set; } = new();

    /// <summary>BG-22: LegalMonetaryTotal (totals summary).</summary>
    [XmlElement("LegalMonetaryTotal", Namespace = UblNamespaces.Cac)]
    public UblMonetaryTotal? LegalMonetaryTotal { get; set; }

    /// <summary>BG-25: Credit note lines.</summary>
    [XmlElement("CreditNoteLine", Namespace = UblNamespaces.Cac)]
    public List<UblInvoiceLine> CreditNoteLines { get; set; } = new();
}
