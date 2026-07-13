using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Models.Compliance;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Service for reporting invoices to the Polish KSeF (Krajowy System e-Faktur) portal.
/// Handles FA(2) XML generation, submission, and status retrieval.
/// </summary>
public interface IPolandKsefService
{
    /// <summary>
    /// Reports an invoice to the KSeF portal.
    /// </summary>
    /// <param name="invoice">The invoice entity to report.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ReportingResult"/> indicating acceptance or rejection.</returns>
    Task<ReportingResult> ReportAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the processing status of a previously submitted invoice.
    /// </summary>
    /// <param name="ksefReference">The KSeF-assigned reference number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ReportingAcknowledgment"/> with the current status.</returns>
    Task<ReportingAcknowledgment> GetStatusAsync(string ksefReference, CancellationToken ct = default);

    /// <summary>
    /// Generates a Polish FA(2) XML document for the given invoice.
    /// </summary>
    /// <param name="invoice">The invoice to format.</param>
    /// <returns>A KSeF-compliant FA(2) XML string.</returns>
    string GenerateFaXml(Invoice invoice);
}
