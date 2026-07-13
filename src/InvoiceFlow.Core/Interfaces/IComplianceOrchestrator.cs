using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Models.Compliance;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Orchestrates compliance processing by routing invoices to the correct
/// country-specific handler based on the configured compliance model.
/// Tracks compliance status, supports post-audit archival hashing, and
/// provides status checking for CTC (Continuous Transaction Control) models.
/// </summary>
public interface IComplianceOrchestrator
{
    /// <summary>
    /// Processes an invoice through the appropriate compliance handler.
    /// Routes to the correct service based on country code or explicit compliance model.
    /// </summary>
    /// <param name="invoice">The invoice to process for compliance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A unified orchestration result wrapping all possible outcomes.</returns>
    Task<ComplianceOrchestrationResult> ProcessAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Checks the current compliance status for CTC models (ItalySdi, FrancePpf, PolandKsef).
    /// For clearance models, returns the cached result from the invoice entity.
    /// </summary>
    /// <param name="invoice">The invoice to check status for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An updated orchestration result with the latest status.</returns>
    Task<ComplianceOrchestrationResult> CheckStatusAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Computes a SHA-256 archival hash of key invoice fields for post-audit immutable records.
    /// </summary>
    /// <param name="invoice">The invoice to hash.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A lowercase hexadecimal SHA-256 hash string.</returns>
    Task<string> ComputeArchivalHashAsync(Invoice invoice, CancellationToken ct = default);
}
