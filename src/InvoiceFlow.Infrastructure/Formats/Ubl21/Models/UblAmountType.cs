using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL amount type with currency ID attribute.</summary>
public class UblAmountType
{
    /// <summary>The monetary amount value.</summary>
    [XmlText]
    public decimal Value { get; set; }

    /// <summary>ISO 4217 currency code (e.g., EUR, USD).</summary>
    [XmlAttribute("currencyID")]
    public string CurrencyId { get; set; } = string.Empty;
}
