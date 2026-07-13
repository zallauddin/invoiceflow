using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Formats.Abstractions;

/// <summary>Writes core invoice entities into a specific electronic format.</summary>
public interface IFormatWriter
{
    /// <summary>The format this writer supports.</summary>
    InvoiceFormat SupportedFormat { get; }

    /// <summary>Write invoice data to the specified format.</summary>
    Task<FormatWriteResult> WriteAsync(Invoice invoice, List<InvoiceLine> lines, CancellationToken ct = default);
}

/// <summary>Result of writing an invoice to a format stream.</summary>
public sealed record FormatWriteResult(
    Stream Content,
    string MediaType,
    string? SuggestedFileName,
    List<ValidationResult> ValidationResults
);
