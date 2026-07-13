using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Cii.Models;

/// <summary>
/// CII MonetarySummation — header-level monetary summation (totals).
/// Maps to SpecifiedTradeSettlementHeaderMonetarySummation.
/// </summary>
public class CiiMonetarySummation
{
    /// <summary>Sum of line total amounts before tax (BT-106).</summary>
    [XmlElement("LineTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? LineTotalAmount { get; set; }

    /// <summary>Tax basis total amount (BT-107, sum of all taxable amounts).</summary>
    [XmlElement("TaxBasisTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? TaxBasisTotalAmount { get; set; }

    /// <summary>Tax total amount (BT-110, total tax across all categories).</summary>
    [XmlElement("TaxTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? TaxTotalAmount { get; set; }

    /// <summary>Grand total amount including tax (BT-112, BT-115).</summary>
    [XmlElement("GrandTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? GrandTotalAmount { get; set; }

    /// <summary>Total allowance amount (document-level discounts, BT-108).</summary>
    [XmlElement("AllowanceTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? AllowanceTotalAmount { get; set; }

    /// <summary>Total charge amount (document-level charges, BT-109).</summary>
    [XmlElement("ChargeTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? ChargeTotalAmount { get; set; }

    /// <summary>Prepaid amount (BT-113).</summary>
    [XmlElement("PrepaidAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? PrepaidAmount { get; set; }

    /// <summary>Amount due for payment (BT-115).</summary>
    [XmlElement("DuePayableAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? DuePayableAmount { get; set; }
}
