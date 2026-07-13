namespace InvoiceFlow.Formats.Abstractions;

/// <summary>Describes a supported invoice format including its capabilities and metadata.</summary>
public sealed record FormatDescriptor(
    InvoiceFormat Format,
    string DisplayName,
    string MediaType,
    string[] FileExtensions,
    string? NamespaceUri,
    string Profile,
    bool IsReadable,
    bool IsWritable
);
