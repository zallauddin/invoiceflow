namespace InvoiceFlow.Infrastructure.Compliance.Zatca.Models;

/// <summary>
/// ZATCA TLV (Tag-Length-Value) tag constants for QR code encoding.
/// Each tag corresponds to a specific field in the Saudi Arabia e-invoicing specification.
/// </summary>
public static class ZatcaTlvTag
{
    /// <summary>Tag 1: Seller Name (UTF-8 text).</summary>
    public const byte SellerName = 0x01;

    /// <summary>Tag 2: VAT Registration Number (UTF-8 text).</summary>
    public const byte VatRegistrationNumber = 0x02;

    /// <summary>Tag 3: Invoice Date/Time in ISO 8601 format (UTF-8 text).</summary>
    public const byte InvoiceTimestamp = 0x03;

    /// <summary>Tag 4: Invoice Total Amount including VAT (UTF-8 text, decimal).</summary>
    public const byte TotalWithVat = 0x04;

    /// <summary>Tag 5: Total VAT Amount (UTF-8 text, decimal).</summary>
    public const byte VatTotal = 0x05;
}
