using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Formats.Abstractions;

/// <summary>Reads an invoice from a specific electronic format into core entity types.</summary>
public interface IFormatReader
{
    /// <summary>The format this reader supports.</summary>
    InvoiceFormat SupportedFormat { get; }

    /// <summary>Read and parse invoice content from a stream.</summary>
    Task<FormatReadResult> ReadAsync(Stream content, CancellationToken ct = default);
}

/// <summary>Result of reading an invoice from a format stream.</summary>
public sealed record FormatReadResult(
    Invoice InvoiceData,
    List<InvoiceLine> Lines,
    string? RawXml,
    Dictionary<string, string> Metadata,
    List<ValidationResult> ValidationResults
);
