using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Models.Compliance;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Service for Indian IRP (Invoice Registration Portal) e-Invoice compliance clearance.
/// </summary>
public interface IIndiaIrpService
{
    /// <summary>
    /// Submits an e-Invoice JSON to the IRP for registration.
    /// </summary>
    Task<ClearanceResult> SubmitEinvoiceAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Generates the GSTN e-Invoice JSON payload for the given invoice.
    /// </summary>
    string GenerateEinvoiceJson(Invoice invoice);
}
