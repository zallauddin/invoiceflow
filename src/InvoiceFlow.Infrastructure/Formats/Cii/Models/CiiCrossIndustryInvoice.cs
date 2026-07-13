using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Cii.Models;

/// <summary>CII ExchangedDocumentContext — guideline and context parameters.</summary>
public class CiiExchangedDocumentContext
{
    /// <summary>Guideline specified document context parameter (e.g., EN 16931).</summary>
    [XmlElement("GuidelineSpecifiedDocumentContextParameter", Namespace = CiiNamespaces.Ram)]
    public CiiGuidelineSpecifiedDocumentContextParameter? GuidelineSpecifiedDocumentContextParameter { get; set; }
}

/// <summary>A document context parameter containing an ID (e.g., profile or guideline ID).</summary>
public class CiiGuidelineSpecifiedDocumentContextParameter
{
    /// <summary>The guideline or profile identifier.</summary>
    [XmlElement("ID", Namespace = CiiNamespaces.Ram)]
    public string? Id { get; set; }
}

/// <summary>CII ExchangedDocument — document-level metadata (ID, type, date, notes).</summary>
public class CiiExchangedDocument
{
    /// <summary>Invoice number / document ID (BT-1, CII-BR-1).</summary>
    [XmlElement("ID", Namespace = CiiNamespaces.Ram)]
    public string? Id { get; set; }

    /// <summary>Document type code: 380 = commercial invoice, 381 = credit note, 220 = order (CII-BR-3).</summary>
    [XmlElement("TypeCode", Namespace = CiiNamespaces.Ram)]
    public string? TypeCode { get; set; }

    /// <summary>Date and time of issue (BT-2, CII-BR-2).</summary>
    [XmlElement("IssueDateTime", Namespace = CiiNamespaces.Ram)]
    public CiiDateTimeType? IssueDateTime { get; set; }

    /// <summary>Included notes (subject code + content).</summary>
    [XmlElement("IncludedNote", Namespace = CiiNamespaces.Ram)]
    public List<CiiNote> IncludedNotes { get; set; } = new();
}

/// <summary>A note attached to the exchanged document.</summary>
public class CiiNote
{
    /// <summary>Note subject code (e.g., AAI = general, REG = regulatory).</summary>
    [XmlElement("SubjectCode", Namespace = CiiNamespaces.Ram)]
    public string? SubjectCode { get; set; }

    /// <summary>Note content text.</summary>
    [XmlElement("Content", Namespace = CiiNamespaces.Ram)]
    public string? Content { get; set; }
}

/// <summary>CII DateTimeType — a date/time with format qualifier (udt:DateTimeString).</summary>
public class CiiDateTimeType
{
    /// <summary>The date/time value as a formatted string.</summary>
    [XmlElement("DateTimeString", Namespace = CiiNamespaces.Udt)]
    public CiiDateTimeString? DateTimeString { get; set; }
}

/// <summary>A date/time string with a format attribute (e.g., 102 = YYYYMMDD, 303 = YYYYMMDDHHMM).</summary>
public class CiiDateTimeString
{
    /// <summary>Format code: 102 = YYYYMMDD, 303 = YYYYMMDDHHMMSS, 616 = YYYY-MM-DD.</summary>
    [XmlAttribute("format")]
    public string Format { get; set; } = "102";

    /// <summary>The date/time value.</summary>
    [XmlText]
    public string Value { get; set; } = string.Empty;
}

/// <summary>CII SupplyChainTradeTransaction — contains header-level trade data and line items.</summary>
public class CiiSupplyChainTradeTransaction
{
    /// <summary>Applicable header trade agreement (seller, buyer, references).</summary>
    [XmlElement("ApplicableHeaderTradeAgreement", Namespace = CiiNamespaces.Ram)]
    public CiiHeaderTradeAgreement? ApplicableHeaderTradeAgreement { get; set; }

    /// <summary>Applicable header trade delivery (delivery event).</summary>
    [XmlElement("ApplicableHeaderTradeDelivery", Namespace = CiiNamespaces.Ram)]
    public CiiHeaderTradeDelivery? ApplicableHeaderTradeDelivery { get; set; }

    /// <summary>Applicable header trade settlement (currency, totals, taxes, payment terms).</summary>
    [XmlElement("ApplicableHeaderTradeSettlement", Namespace = CiiNamespaces.Ram)]
    public CiiHeaderTradeSettlement? ApplicableHeaderTradeSettlement { get; set; }

    /// <summary>Included supply chain trade line items.</summary>
    [XmlElement("IncludedSupplyChainTradeLineItem", Namespace = CiiNamespaces.Ram)]
    public List<CiiTradeLineItem> IncludedSupplyChainTradeLineItems { get; set; } = new();
}

/// <summary>CII HeaderTradeAgreement — seller, buyer, buyer reference, and agreement details.</summary>
public class CiiHeaderTradeAgreement
{
    /// <summary>Buyer reference (project reference, PO number, BT-10).</summary>
    [XmlElement("BuyerReference", Namespace = CiiNamespaces.Ram)]
    public string? BuyerReference { get; set; }

    /// <summary>Seller trade party (BT-27+).</summary>
    [XmlElement("SellerTradeParty", Namespace = CiiNamespaces.Ram)]
    public CiiTradeParty? SellerTradeParty { get; set; }

    /// <summary>Buyer trade party (BT-44+).</summary>
    [XmlElement("BuyerTradeParty", Namespace = CiiNamespaces.Ram)]
    public CiiTradeParty? BuyerTradeParty { get; set; }
}

/// <summary>CII HeaderTradeDelivery — delivery event information.</summary>
public class CiiHeaderTradeDelivery
{
    /// <summary>Actual delivery supply chain event.</summary>
    [XmlElement("ActualDeliverySupplyChainEvent", Namespace = CiiNamespaces.Ram)]
    public CiiSupplyChainEvent? ActualDeliverySupplyChainEvent { get; set; }
}

/// <summary>A supply chain event with an occurrence date/time.</summary>
public class CiiSupplyChainEvent
{
    /// <summary>The occurrence date/time.</summary>
    [XmlElement("OccurrenceDateTime", Namespace = CiiNamespaces.Ram)]
    public CiiDateTimeType? OccurrenceDateTime { get; set; }
}

/// <summary>
/// CII HeaderTradeSettlement — invoice currency, monetary summation, payment terms, and taxes.
/// </summary>
public class CiiHeaderTradeSettlement
{
    /// <summary>Invoice currency code (BT-5, ISO 4217).</summary>
    [XmlElement("InvoiceCurrencyCode", Namespace = CiiNamespaces.Ram)]
    public string? InvoiceCurrencyCode { get; set; }

    /// <summary>Tax currency code (BT-6, ISO 4217, optional).</summary>
    [XmlElement("TaxCurrencyCode", Namespace = CiiNamespaces.Ram)]
    public string? TaxCurrencyCode { get; set; }

    /// <summary>Specified trade settlement header monetary summation (totals).</summary>
    [XmlElement("SpecifiedTradeSettlementHeaderMonetarySummation", Namespace = CiiNamespaces.Ram)]
    public CiiMonetarySummation? SpecifiedTradeSettlementHeaderMonetarySummation { get; set; }

    /// <summary>Specified trade payment terms.</summary>
    [XmlElement("SpecifiedTradePaymentTerms", Namespace = CiiNamespaces.Ram)]
    public CiiTradePaymentTerms? SpecifiedTradePaymentTerms { get; set; }

    /// <summary>Applicable trade tax (one per tax rate/category).</summary>
    [XmlElement("ApplicableTradeTax", Namespace = CiiNamespaces.Ram)]
    public List<CiiTradeTax> ApplicableTradeTaxes { get; set; } = new();
}

/// <summary>CII TradePaymentTerms — payment terms description and due date.</summary>
public class CiiTradePaymentTerms
{
    /// <summary>Payment terms description (BT-20).</summary>
    [XmlElement("Description", Namespace = CiiNamespaces.Ram)]
    public string? Description { get; set; }

    /// <summary>Due date for payment (BT-9).</summary>
    [XmlElement("DueDateDateTime", Namespace = CiiNamespaces.Ram)]
    public CiiDateTimeType? DueDateDateTime { get; set; }
}

/// <summary>Root CII CrossIndustryInvoice document model — UN/CEFACT D10B.</summary>
[XmlRoot("CrossIndustryInvoice", Namespace = CiiNamespaces.Rsm)]
public class CiiCrossIndustryInvoice
{
    /// <summary>Document context (guideline profile, e.g., EN 16931).</summary>
    [XmlElement("ExchangedDocumentContext", Namespace = CiiNamespaces.Rsm)]
    public CiiExchangedDocumentContext? ExchangedDocumentContext { get; set; }

    /// <summary>Exchanged document (ID, type code, issue date, notes).</summary>
    [XmlElement("ExchangedDocument", Namespace = CiiNamespaces.Rsm)]
    public CiiExchangedDocument? ExchangedDocument { get; set; }

    /// <summary>Supply chain trade transaction (agreement, delivery, settlement, line items).</summary>
    [XmlElement("SupplyChainTradeTransaction", Namespace = CiiNamespaces.Rsm)]
    public CiiSupplyChainTradeTransaction? SupplyChainTradeTransaction { get; set; }
}
