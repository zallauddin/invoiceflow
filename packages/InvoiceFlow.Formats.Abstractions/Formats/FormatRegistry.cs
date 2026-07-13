namespace InvoiceFlow.Formats.Abstractions;

/// <summary>Concrete implementation of <see cref="IFormatRegistry"/> using dictionary-based storage.</summary>
public sealed class FormatRegistry : IFormatRegistry
{
    private readonly Dictionary<InvoiceFormat, FormatEntry> _entries = new();

    /// <inheritdoc />
    public IReadOnlyList<FormatDescriptor> AllFormats =>
        _entries.Values.Select(e => e.Descriptor).ToList().AsReadOnly();

    /// <inheritdoc />
    public void Register(FormatDescriptor descriptor, IFormatReader? reader = null, IFormatWriter? writer = null, IFormatValidator? validator = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _entries[descriptor.Format] = new FormatEntry(descriptor, reader, writer, validator);
    }

    /// <inheritdoc />
    public FormatDescriptor? GetDescriptor(InvoiceFormat format)
    {
        return _entries.TryGetValue(format, out var entry) ? entry.Descriptor : null;
    }

    /// <inheritdoc />
    public IFormatReader? GetReader(InvoiceFormat format)
    {
        return _entries.TryGetValue(format, out var entry) ? entry.Reader : null;
    }

    /// <inheritdoc />
    public IFormatWriter? GetWriter(InvoiceFormat format)
    {
        return _entries.TryGetValue(format, out var entry) ? entry.Writer : null;
    }

    /// <inheritdoc />
    public IFormatValidator? GetValidator(InvoiceFormat format)
    {
        return _entries.TryGetValue(format, out var entry) ? entry.Validator : null;
    }

    /// <inheritdoc />
    public InvoiceFormat DetectFormat(Stream content, string? fileName = null)
    {
        // Try extension-based detection first if a filename is provided
        if (!string.IsNullOrEmpty(fileName))
        {
            var byExtension = FormatDetection.DetectFromFileName(fileName);
            if (byExtension != InvoiceFormat.Unknown)
            {
                return byExtension;
            }
        }

        // Try content-based detection (XML namespace sniffing)
        if (content.CanRead && content.Length > 0)
        {
            var originalPosition = content.Position;
            try
            {
                content.Position = 0;
                return FormatDetection.DetectFromStream(content);
            }
            finally
            {
                content.Position = originalPosition;
            }
        }

        return InvoiceFormat.Unknown;
    }

    /// <inheritdoc />
    public InvoiceFormat DetectFormat(string fileName)
    {
        return FormatDetection.DetectFromFileName(fileName);
    }

    private sealed record FormatEntry(
        FormatDescriptor Descriptor,
        IFormatReader? Reader,
        IFormatWriter? Writer,
        IFormatValidator? Validator
    );
}
