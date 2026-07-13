using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Models.Compliance;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Service for validating, generating, and transmitting PEPPOL BIS 3.0 compliant invoices.
/// </summary>
public interface IPeppolComplianceService
{
    /// <summary>
    /// Validates an invoice against PEPPOL BIS 3.0 business rules.
    /// </summary>
    /// <param name="invoice">The invoice to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A validation result indicating pass/fail with errors and warnings.</returns>
    Task<PeppolValidationResult> ValidateAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Transmits a PEPPOL-compliant UBL document to the configured Access Point.
    /// </summary>
    /// <param name="invoice">The invoice being transmitted.</param>
    /// <param name="ublXml">The UBL 2.1 XML content to transmit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A transmission result indicating success/failure with Access Point details.</returns>
    Task<PeppolTransmissionResult> TransmitAsync(Invoice invoice, string ublXml, CancellationToken ct = default);

    /// <summary>
    /// Generates PEPPOL BIS 3.0 compliant UBL 2.1 XML from an invoice entity.
    /// </summary>
    /// <param name="invoice">The invoice to serialize to UBL XML.</param>
    /// <returns>A string containing the UBL 2.1 XML document.</returns>
    string GenerateUblXml(Invoice invoice);
}
