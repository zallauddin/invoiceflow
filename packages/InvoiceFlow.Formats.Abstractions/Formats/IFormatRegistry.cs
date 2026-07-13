namespace InvoiceFlow.Formats.Abstractions;

/// <summary>Central registry for all format descriptors, readers, writers, and validators.</summary>
public interface IFormatRegistry
{
    /// <summary>Register a format with its associated reader, writer, and validator.</summary>
    void Register(FormatDescriptor descriptor, IFormatReader? reader = null, IFormatWriter? writer = null, IFormatValidator? validator = null);

    /// <summary>Get the format descriptor for the specified format.</summary>
    FormatDescriptor? GetDescriptor(InvoiceFormat format);

    /// <summary>Get the format reader for the specified format.</summary>
    IFormatReader? GetReader(InvoiceFormat format);

    /// <summary>Get the format writer for the specified format.</summary>
    IFormatWriter? GetWriter(InvoiceFormat format);

    /// <summary>Get the format validator for the specified format.</summary>
    IFormatValidator? GetValidator(InvoiceFormat format);

    /// <summary>All registered format descriptors.</summary>
    IReadOnlyList<FormatDescriptor> AllFormats { get; }

    /// <summary>Detect the invoice format from a stream by inspecting content and optional filename.</summary>
    InvoiceFormat DetectFormat(Stream content, string? fileName = null);

    /// <summary>Detect the invoice format from a file name alone (extension-based).</summary>
    InvoiceFormat DetectFormat(string fileName);
}
