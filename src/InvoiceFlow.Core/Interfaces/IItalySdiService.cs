using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Models.Compliance;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Service for transmitting invoices to the Italian SdI (Sistema di Interscambio) portal.
/// Handles FatturaPA XML generation, transmission, and status checking.
/// </summary>
public interface IItalySdiService
{
    /// <summary>
    /// Transmits an invoice to the SdI portal as a FatturaPA XML document.
    /// </summary>
    /// <param name="invoice">The invoice entity to transmit.</param>
    /// <param name="fatturaPaXml">Pre-generated FatturaPA XML string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ReportingResult"/> indicating acceptance or rejection.</returns>
    Task<ReportingResult> TransmitAsync(Invoice invoice, string fatturaPaXml, CancellationToken ct = default);

    /// <summary>
    /// Checks the current processing status of a previously transmitted invoice.
    /// </summary>
    /// <param name="sdiIdentifier">The SdI-assigned identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ReportingAcknowledgment"/> with the current status.</returns>
    Task<ReportingAcknowledgment> CheckStatusAsync(string sdiIdentifier, CancellationToken ct = default);

    /// <summary>
    /// Formats an invoice as FatturaPA XML for SdI transmission.
    /// </summary>
    /// <param name="invoice">The invoice to format.</param>
    /// <returns>A FatturaPA-compliant XML string.</returns>
    string FormatFatturaPaForSdi(Invoice invoice);
}
