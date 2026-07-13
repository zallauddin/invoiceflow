using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

/// <summary>
/// CII SpecifiedTradeSettlementHeaderMonetarySummation (ram) — header-level financial totals.
/// Maps to EN 16931 business terms BT-106 through BT-115.
/// </summary>
public class CiiMonetarySummation
{
    /// <summary>Tax basis total amount — sum of all line net amounts (BT-106).</summary>
    [XmlElement("TaxBasisTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? TaxBasisTotalAmount { get; set; }

    /// <summary>Tax total amount — total tax across all categories (BT-110).</summary>
    [XmlElement("TaxTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? TaxTotalAmount { get; set; }

    /// <summary>Grand total amount — tax basis + tax total (BT-112).</summary>
    [XmlElement("GrandTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? GrandTotalAmount { get; set; }

    /// <summary>Due payable amount — the amount to be paid (BT-115).</summary>
    [XmlElement("DuePayableAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? DuePayableAmount { get; set; }
}
