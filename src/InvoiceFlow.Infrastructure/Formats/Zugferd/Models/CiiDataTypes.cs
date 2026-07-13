using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

// ─────────────────────────────────────────────────────────────
// Qualified data types (udt / qdt)
// ─────────────────────────────────────────────────────────────

/// <summary>
/// CII date-time qualified type — wraps a DateTimeString with format attribute.
/// CII uses format "102" (yyyyMMdd) for dates.
/// </summary>
public class CiiDateTimeType
{
    /// <summary>The date/time value as a formatted string.</summary>
    [XmlElement("DateTimeString", Namespace = CiiNamespaces.Udt)]
    public CiiDateTimeString? DateTimeString { get; set; }
}

/// <summary>
/// A date/time string with format code attribute (udt:DateTimeString).
/// </summary>
public class CiiDateTimeString
{
    /// <summary>Format code: 102 = yyyyMMdd, 610 = yyyyMMddHHmm, 203 = yyyyMMddHHmmss.</summary>
    [XmlAttribute("format")]
    public string Format { get; set; } = "102";

    /// <summary>The formatted date/time value.</summary>
    [XmlText]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// CII amount type — decimal value with currencyID attribute (udt:AmountType).
/// Used for amounts that carry a currency code.
/// </summary>
public class CiiAmountType
{
    /// <summary>The monetary amount.</summary>
    [XmlText]
    public decimal Value { get; set; }

    /// <summary>ISO 4217 currency code (e.g., EUR, USD).</summary>
    [XmlAttribute("currencyID")]
    public string CurrencyId { get; set; } = string.Empty;
}

/// <summary>
/// CII amount type without currency attribute — for percentage-related amounts.
/// Used in elements like BasisAmount and CalculatedAmount within tax breakdown.
/// </summary>
public class CiiAmountWithoutCurrencyType
{
    /// <summary>The monetary amount.</summary>
    [XmlText]
    public decimal Value { get; set; }
}

/// <summary>
/// CII rate type — decimal percentage value (udt:PercentType).
/// Used for tax rates like 19.0%.
/// </summary>
public class CiiPercentType
{
    /// <summary>The percentage value.</summary>
    [XmlText]
    public decimal Value { get; set; }
}

/// <summary>
/// CII quantity type — decimal value with unitCode attribute (udt:QuantityType).
/// </summary>
public class CiiQuantityType
{
    /// <summary>The quantity value.</summary>
    [XmlText]
    public decimal Value { get; set; }

    /// <summary>UN/ECE Rec. 20 unit code (e.g., EA, H, KGM).</summary>
    [XmlAttribute("unitCode")]
    public string? UnitCode { get; set; }
}
