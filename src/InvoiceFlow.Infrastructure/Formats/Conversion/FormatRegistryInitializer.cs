using InvoiceFlow.Formats.Abstractions;
using Microsoft.Extensions.Hosting;

namespace InvoiceFlow.Infrastructure.Formats.Conversion;

/// <summary>
/// Hosted service that populates the <see cref="IFormatRegistry"/> with well-known
/// <see cref="FormatDescriptor"/> instances and matches them to their corresponding
/// readers, writers, and validators at application startup.
/// </summary>
public class FormatRegistryInitializer : IHostedService
{
    private readonly IFormatRegistry _registry;
    private readonly IEnumerable<IFormatReader> _readers;
    private readonly IEnumerable<IFormatWriter> _writers;
    private readonly IEnumerable<IFormatValidator> _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatRegistryInitializer"/> class.
    /// </summary>
    public FormatRegistryInitializer(
        IFormatRegistry registry,
        IEnumerable<IFormatReader> readers,
        IEnumerable<IFormatWriter> writers,
        IEnumerable<IFormatValidator> validators)
    {
        _registry = registry;
        _readers = readers;
        _writers = writers;
        _validators = validators;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var readerMap = _readers.ToDictionary(r => r.SupportedFormat);
        var writerMap = _writers.ToDictionary(w => w.SupportedFormat);
        var validatorMap = _validators.ToDictionary(v => v.SupportedFormat);

        foreach (var descriptor in GetAllDescriptors())
        {
            cancellationToken.ThrowIfCancellationRequested();

            readerMap.TryGetValue(descriptor.Format, out var reader);
            writerMap.TryGetValue(descriptor.Format, out var writer);
            validatorMap.TryGetValue(descriptor.Format, out var validator);

            _registry.Register(descriptor, reader, writer, validator);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IEnumerable<FormatDescriptor> GetAllDescriptors()
    {
        yield return new FormatDescriptor(
            Format: InvoiceFormat.Ubl21,
            DisplayName: "UBL 2.1 Invoice",
            MediaType: "application/vnd.ubl21+xml",
            FileExtensions: [".xml"],
            NamespaceUri: "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
            Profile: "EN16931",
            IsReadable: true,
            IsWritable: true);

        yield return new FormatDescriptor(
            Format: InvoiceFormat.XRechnung,
            DisplayName: "XRechnung 3.0",
            MediaType: "application/vnd.bund.xrechnung+xml",
            FileExtensions: [".xml"],
            NamespaceUri: "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
            Profile: "XRechnung",
            IsReadable: true,
            IsWritable: true);

        yield return new FormatDescriptor(
            Format: InvoiceFormat.Zugferd,
            DisplayName: "ZUGFeRD / Factur-X",
            MediaType: "application/pdf",
            FileExtensions: [".pdf", ".factur-x.pdf", ".zugferd.pdf"],
            NamespaceUri: null,
            Profile: "ZUGFeRD",
            IsReadable: true,
            IsWritable: true);

        yield return new FormatDescriptor(
            Format: InvoiceFormat.FatturaPA,
            DisplayName: "FatturaPA v1.2",
            MediaType: "application/vnd.fatturapa+xml",
            FileExtensions: [".xml", ".p7m", ".p7c"],
            NamespaceUri: "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2",
            Profile: "FatturaPA",
            IsReadable: true,
            IsWritable: true);

        yield return new FormatDescriptor(
            Format: InvoiceFormat.EbInterface,
            DisplayName: "ebInterface",
            MediaType: "application/vnd.ebinterface+xml",
            FileExtensions: [".xml"],
            NamespaceUri: "http://www.ebinterface.gv.at/namespace",
            Profile: "ebInterface",
            IsReadable: true,
            IsWritable: true);

        yield return new FormatDescriptor(
            Format: InvoiceFormat.Cii,
            DisplayName: "UN/CEFACT Cross-Industry Invoice",
            MediaType: "application/vnd.cii+xml",
            FileExtensions: [".xml"],
            NamespaceUri: "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100",
            Profile: "CII",
            IsReadable: true,
            IsWritable: false);
    }
}
