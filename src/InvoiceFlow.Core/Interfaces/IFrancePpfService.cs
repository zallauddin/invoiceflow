using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Models.Compliance;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Service for reporting invoices to the French PPF (Portail Public de Facturation) portal.
/// Handles Factur-X XML generation, submission, and acknowledgment retrieval.
/// </summary>
public interface IFrancePpfService
{
    /// <summary>
    /// Reports an invoice to the French PPF portal.
    /// </summary>
    /// <param name="invoice">The invoice entity to report.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ReportingResult"/> indicating acceptance or rejection.</returns>
    Task<ReportingResult> ReportAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the acknowledgment status for a previously submitted invoice.
    /// </summary>
    /// <param name="ppfReference">The PPF-assigned reference number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ReportingAcknowledgment"/> with the current status.</returns>
    Task<ReportingAcknowledgment> GetAcknowledgmentAsync(string ppfReference, CancellationToken ct = default);

    /// <summary>
    /// Generates a Factur-X (CII) XML document for the given invoice.
    /// </summary>
    /// <param name="invoice">The invoice to format.</param>
    /// <returns>A Factur-X-compliant XML string.</returns>
    string GenerateFacturXXml(Invoice invoice);
}
