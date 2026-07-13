using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 Invoice root document — maps to the &lt;Invoice&gt; XML root element.
/// Models the core invoice structure per EN 16931 / Peppol BIS Billing 3.0.</summary>
[XmlRoot("Invoice", Namespace = UblNamespaces.Invoice)]
public class UblInvoice
{
    [XmlAttribute("xmlns:cbc", Namespace = UblNamespaces.Cbc)]
    public string XmlNsCbc { get; set; } = string.Empty;

    [XmlAttribute("xmlns:cac", Namespace = UblNamespaces.Cac)]
    public string XmlNsCac { get; set; } = string.Empty;

    // --- CBC (Common Basic Components) ---

    /// <summary>BT-24: UBL version identifier. Fixed to "2.1".</summary>
    [XmlElement("UBLVersionID", Namespace = UblNamespaces.Cbc)]
    public string? UblVersionId { get; set; } = "2.1";

    /// <summary>BT-22: Customization identifier (e.g., EN 16931).</summary>
    [XmlElement("CustomizationID", Namespace = UblNamespaces.Cbc)]
    public string? CustomizationId { get; set; }

    /// <summary>BT-23: Profile identifier (e.g., Peppol Billing).</summary>
    [XmlElement("ProfileID", Namespace = UblNamespaces.Cbc)]
    public string? ProfileId { get; set; }

    /// <summary>BT-5: Profile execution identifier (optional).</summary>
    [XmlElement("ProfileExecutionID", Namespace = UblNamespaces.Cbc)]
    public string? ProfileExecutionId { get; set; }

    /// <summary>BT-1: Invoice number (mandatory).</summary>
    [XmlElement("ID", Namespace = UblNamespaces.Cbc)]
    public string? Id { get; set; }

    /// <summary>BT-2: Invoice issue date (mandatory).</summary>
    [XmlElement("IssueDate", Namespace = UblNamespaces.Cbc)]
    public DateTime? IssueDate { get; set; }

    /// <summary>BT-9: Invoice due date.</summary>
    [XmlElement("DueDate", Namespace = UblNamespaces.Cbc)]
    public DateTime? DueDate { get; set; }

    /// <summary>BT-3: Invoice type code (mandatory, e.g., 380 for Invoice, 381 for Credit Note).</summary>
    [XmlElement("InvoiceTypeCode", Namespace = UblNamespaces.Cbc)]
    public string? InvoiceTypeCode { get; set; }

    /// <summary>BT-3-1: Invoice type code name (optional, human-readable).</summary>
    [XmlElement("InvoiceTypeCodeName", Namespace = UblNamespaces.Cbc)]
    public string? InvoiceTypeCodeName { get; set; }

    /// <summary>BT-5: Invoice currency code (mandatory, ISO 4217).</summary>
    [XmlElement("DocumentCurrencyCode", Namespace = UblNamespaces.Cbc)]
    public string? DocumentCurrencyCode { get; set; }

    /// <summary>BT-6: Tax currency code (optional, ISO 4217).</summary>
    [XmlElement("TaxCurrencyCode", Namespace = UblNamespaces.Cbc)]
    public string? TaxCurrencyCode { get; set; }

    /// <summary>BT-8: Invoice accounting cost reference.</summary>
    [XmlElement("AccountingCost", Namespace = UblNamespaces.Cbc)]
    public string? AccountingCost { get; set; }

    /// <summary>BT-11: Buyer reference (project reference, PO number).</summary>
    [XmlElement("BuyerReference", Namespace = UblNamespaces.Cbc)]
    public string? BuyerReference { get; set; }

    // --- CAC (Common Aggregate Components) ---

    /// <summary>BT-27: AccountingSupplierParty (seller/vendor).</summary>
    [XmlElement("AccountingSupplierParty", Namespace = UblNamespaces.Cac)]
    public UblSupplierParty? AccountingSupplierParty { get; set; }

    /// <summary>BT-44: AccountingCustomerParty (buyer).</summary>
    [XmlElement("AccountingCustomerParty", Namespace = UblNamespaces.Cac)]
    public UblCustomerParty? AccountingCustomerParty { get; set; }

    /// <summary>Payment to the Payee (optional).</summary>
    [XmlElement("PayeeParty", Namespace = UblNamespaces.Cac)]
    public UblPayeeParty? PayeeParty { get; set; }

    /// <summary>BT-20: Payment instructions.</summary>
    [XmlElement("PaymentMeans", Namespace = UblNamespaces.Cac)]
    public UblPaymentMeans? PaymentMeans { get; set; }

    /// <summary>BT-151+: Payment terms.</summary>
    [XmlElement("PaymentTerms", Namespace = UblNamespaces.Cac)]
    public UblPaymentTerms? PaymentTerms { get; set; }

    /// <summary>BG-23: Allowances (document level).</summary>
    [XmlElement("AllowanceCharge", Namespace = UblNamespaces.Cac)]
    public List<UblAllowanceCharge> AllowanceCharges { get; set; } = new();

    /// <summary>BT-110+: TaxTotal (one per tax type).</summary>
    [XmlElement("TaxTotal", Namespace = UblNamespaces.Cac)]
    public List<UblTaxTotal> TaxTotals { get; set; } = new();

    /// <summary>BG-22: LegalMonetaryTotal (totals summary).</summary>
    [XmlElement("LegalMonetaryTotal", Namespace = UblNamespaces.Cac)]
    public UblMonetaryTotal? LegalMonetaryTotal { get; set; }

    /// <summary>BG-25: Invoice lines.</summary>
    [XmlElement("InvoiceLine", Namespace = UblNamespaces.Cac)]
    public List<UblInvoiceLine> InvoiceLines { get; set; } = new();
}
