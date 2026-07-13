using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Models.Compliance;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Service for Brazilian NF-e (Nota Fiscal Eletrônica) compliance clearance via SEFAZ.
/// </summary>
public interface IBrazilNfeService
{
    /// <summary>
    /// Submits an NF-e to the SEFAZ web service for clearance.
    /// </summary>
    Task<ClearanceResult> SubmitNfeAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Generates the NF-e XML document for the given invoice.
    /// </summary>
    string GenerateNfeXml(Invoice invoice);

    /// <summary>
    /// Computes the SHA-1 digest of the NF-e XML content (Base64-encoded).
    /// </summary>
    string ComputeNfeDigest(string xmlContent);
}
