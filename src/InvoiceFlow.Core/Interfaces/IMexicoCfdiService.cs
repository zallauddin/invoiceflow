using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Models.Compliance;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Service for Mexican CFDI (Comprobante Fiscal Digital por Internet) compliance stamping via a PAC.
/// </summary>
public interface IMexicoCfdiService
{
    /// <summary>
    /// Submits a CFDI XML to a PAC for digital stamping and SAT registration.
    /// </summary>
    Task<ClearanceResult> StampCfdiAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Generates the CFDI 4.0 XML document for the given invoice.
    /// </summary>
    string GenerateCfdiXml(Invoice invoice);

    /// <summary>
    /// Computes the SHA-256 digest of the CFDI XML content (Base64-encoded).
    /// </summary>
    string ComputeCfdiDigest(string xmlContent);
}
