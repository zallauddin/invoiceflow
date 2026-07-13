using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Models.Compliance;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Saudi Arabia ZATCA (Zakat, Tax and Customs Authority) e-invoicing compliance service.
/// Handles FATOORAH XML generation, TLV QR code encoding, invoice hashing, and clearance requests.
/// </summary>
public interface IZatcaComplianceService
{
    /// <summary>
    /// Requests clearance of an invoice through the ZATCA API.
    /// Generates FATOORAH XML, computes the invoice hash, and submits for clearance.
    /// </summary>
    /// <param name="invoice">The invoice to request clearance for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A clearance result containing the status, identifiers, and QR code data.</returns>
    Task<ZatcaClearanceResult> RequestClearanceAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Generates the ZATCA FATOORAH XML representation of an invoice.
    /// </summary>
    /// <param name="invoice">The invoice to serialize.</param>
    /// <returns>A valid FATOORAH XML string conforming to the ZATCA schema.</returns>
    string GenerateFatoorahXml(Invoice invoice);

    /// <summary>
    /// Generates a ZATCA-compliant TLV (Tag-Length-Value) QR code as a Base64 string.
    /// Encodes seller name, VAT number, timestamp, total with VAT, and VAT total.
    /// </summary>
    /// <param name="invoice">The invoice to encode.</param>
    /// <returns>Base64-encoded TLV QR code data.</returns>
    string GenerateTlvQrCode(Invoice invoice);

    /// <summary>
    /// Computes the SHA-256 hash of the FATOORAH XML content, encoded as Base64.
    /// </summary>
    /// <param name="xmlContent">The raw XML string to hash.</param>
    /// <returns>Base64-encoded SHA-256 hash of the XML content.</returns>
    string ComputeInvoiceHash(string xmlContent);
}
