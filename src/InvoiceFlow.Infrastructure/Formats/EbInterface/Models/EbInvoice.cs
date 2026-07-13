using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.EbInterface.Models;

/// <summary>ebInterface root invoice document — maps to the &lt;Invoice&gt; XML root element.
/// Models the core ebInterface structure per Austrian e-invoicing specification 6.0.</summary>
[XmlRoot("Invoice", Namespace = EbInterfaceNamespaces.V6)]
public class EbInvoice
{
    /// <summary>Name of the system that generated this invoice (optional).</summary>
    [XmlElement("GeneratingSystem")]
    public string? GeneratingSystem { get; set; }

    /// <summary>Invoice number assigned by the biller (mandatory).</summary>
    [XmlElement("InvoiceNumber")]
    public string? InvoiceNumber { get; set; }

    /// <summary>Date of invoice issuance (mandatory).</summary>
    [XmlElement("InvoiceDate")]
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Type of document: Invoice, CreditNote, FinalSettlement, or Correction.</summary>
    [XmlElement("DocumentType")]
    public EbDocumentType DocumentType { get; set; } = EbDocumentType.Invoice;

    /// <summary>Delivery date, if applicable (optional).</summary>
    [XmlElement("DeliveryDate")]
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Approval identifier (Genehmigungszeichen) for regulated professions (optional).</summary>
    [XmlElement("GZ")]
    public string? ApprovalIdentifier { get; set; }

    /// <summary>Free-text comment on the invoice (optional).</summary>
    [XmlElement("Comment")]
    public string? Comment { get; set; }

    /// <summary>Invoice issuer / supplier (mandatory).</summary>
    [XmlElement("Biller")]
    public EbParty? Biller { get; set; }

    /// <summary>Invoice recipient / buyer (mandatory).</summary>
    [XmlElement("InvoiceRecipient")]
    public EbParty? InvoiceRecipient { get; set; }

    /// <summary>List of line items on the invoice.</summary>
    [XmlArray("ListLineItem")]
    [XmlArrayItem("LineItem")]
    public List<EbLineItem> ListLineItem { get; set; } = new();

    /// <summary>Document-level tax summary — total tax amount and type.</summary>
    [XmlElement("Tax")]
    public EbTaxItem? Tax { get; set; }

    /// <summary>Total net amount before tax (mandatory).</summary>
    [XmlElement("TotalNetAmount")]
    public decimal TotalNetAmount { get; set; }

    /// <summary>Total gross amount including tax (mandatory).</summary>
    [XmlElement("TotalGrossAmount")]
    public decimal TotalGrossAmount { get; set; }

    /// <summary>Reason for tax exemption, if applicable (optional).</summary>
    [XmlElement("TaxExemptionReason")]
    public string? TaxExemptionReason { get; set; }
}
