using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Cii.Models;

/// <summary>CII TradeTax — trade tax details (rate, category, amounts).</summary>
public class CiiTradeTax
{
    /// <summary>Calculated tax amount (BT-111 per rate).</summary>
    [XmlElement("CalculatedAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? CalculatedAmount { get; set; }

    /// <summary>Basis amount (taxable base, BT-106/BT-116 per rate).</summary>
    [XmlElement("BasisAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? BasisAmount { get; set; }

    /// <summary>Applicable percent / tax rate (BT-116/BT-152, e.g., 19.0).</summary>
    [XmlElement("ApplicablePercent", Namespace = CiiNamespaces.Ram)]
    public decimal? ApplicablePercent { get; set; }

    /// <summary>
    /// Tax category code (BT-118/BT-151): S = standard, Z = zero rate, E = exempt,
    /// AE = reverse charge, K = charge, G = export, O = outside scope, L = low rate,
    /// M = margin scheme.
    /// </summary>
    [XmlElement("CategoryCode", Namespace = CiiNamespaces.Ram)]
    public string? CategoryCode { get; set; }

    /// <summary>Exemption reason text (BT-120/BT-161, optional).</summary>
    [XmlElement("ExemptionReason", Namespace = CiiNamespaces.Ram)]
    public string? ExemptionReason { get; set; }

    /// <summary>Tax type code (e.g., VAT for value-added tax).</summary>
    [XmlElement("TypeCode", Namespace = CiiNamespaces.Ram)]
    public string? TypeCode { get; set; }
}
