using System.Globalization;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.EbInterface.Models;

namespace InvoiceFlow.Infrastructure.Formats.EbInterface;

/// <summary>
/// Reads an ebInterface XML stream (versions 4.3, 5.0, 6.0) and maps it to core entity types.
/// Supports Invoice, CreditNote, FinalSettlement, and Correction document types.
/// </summary>
public sealed class EbInterfaceFormatReader : IFormatReader
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.EbInterface;

    /// <summary>Read and parse an ebInterface XML stream into Invoice + InvoiceLine entities.</summary>
    public async Task<FormatReadResult> ReadAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Read stream into memory so we can inspect root element and deserialize
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Read raw XML string for rawXml output and format detection
        using var reader = new StreamReader(ms, leaveOpen: true);
        var rawXml = await reader.ReadToEndAsync(ct);
        ms.Position = 0;

        // Detect version from namespace and deserialize
        var detectedVersion = DetectVersion(rawXml);
        var ebInvoice = DeserializeFromStream<EbInvoice>(ms);

        if (ebInvoice is null)
        {
            throw new InvalidOperationException("Failed to deserialize ebInterface XML: root element could not be parsed.");
        }

        // Map to core entities
        var coreInvoice = MapToInvoice(ebInvoice);
        var lines = MapToLines(ebInvoice.ListLineItem, coreInvoice.Id);

        // Collect validation results
        var validationResults = new List<ValidationResult>();
        ValidateVersion(detectedVersion, validationResults);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (detectedVersion is not null)
            metadata["ebInterfaceVersion"] = detectedVersion;
        if (ebInvoice.GeneratingSystem is not null or { Length: > 0 })
            metadata["GeneratingSystem"] = ebInvoice.GeneratingSystem!;
        if (ebInvoice.ApprovalIdentifier is not null or { Length: > 0 })
            metadata["ApprovalIdentifier"] = ebInvoice.ApprovalIdentifier!;

        return new FormatReadResult(coreInvoice, lines, rawXml, metadata, validationResults);
    }

    /// <summary>Map an ebInterface EbInvoice to a core Invoice entity.</summary>
    private static Invoice MapToInvoice(EbInvoice eb)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = eb.InvoiceNumber ?? string.Empty,
            InvoiceDate = eb.InvoiceDate ?? DateTime.MinValue,
            Currency = "EUR" // ebInterface is primarily EUR-based (Austrian standard)
        };

        // Map document type
        invoice.DocumentType = eb.DocumentType switch
        {
            EbDocumentType.CreditNote => DocumentType.CreditNote,
            EbDocumentType.FinalSettlement => DocumentType.Invoice, // No direct enum equivalent
            EbDocumentType.Correction => DocumentType.Invoice, // No direct enum equivalent
            _ => DocumentType.Invoice
        };

        // Biller (supplier / vendor)
        invoice.VendorName = eb.Biller?.Name
            ?? eb.Biller?.TradeName
            ?? string.Empty;
        invoice.VendorTaxId = eb.Biller?.VATIdentificationNumber;

        // InvoiceRecipient (buyer / customer)
        invoice.BuyerName = eb.InvoiceRecipient?.Name
            ?? eb.InvoiceRecipient?.TradeName
            ?? string.Empty;
        invoice.BuyerTaxId = eb.InvoiceRecipient?.VATIdentificationNumber;

        // Financials
        invoice.Subtotal = eb.TotalNetAmount;
        invoice.TotalAmount = eb.TotalGrossAmount;
        invoice.TaxAmount = eb.Tax?.Amount ?? Math.Max(0, eb.TotalGrossAmount - eb.TotalNetAmount);

        // Delivery date → DueDate if delivery date is present
        if (eb.DeliveryDate.HasValue)
        {
            invoice.DueDate = eb.DeliveryDate;
        }

        // Comment → Notes
        invoice.Notes = eb.Comment;

        return invoice;
    }

    /// <summary>Map ebInterface line items to core InvoiceLine entities.</summary>
    private static List<InvoiceLine> MapToLines(List<EbLineItem> ebLines, Guid invoiceId)
    {
        var lines = new List<InvoiceLine>(ebLines.Count);
        foreach (var ebLine in ebLines)
        {
            var line = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                LineNumber = ebLine.LineNumber,
                Description = ebLine.Description ?? string.Empty,
                Quantity = ebLine.Quantity?.Value ?? 0m,
                Unit = ebLine.Quantity?.Unit,
                UnitPrice = ebLine.UnitPrice,
                LineTotal = ebLine.LineTotalAmount,
                TaxRate = ebLine.Tax?.TaxRate ?? 0m,
                TaxAmount = ebLine.Tax?.Taxes.Sum(t => t.Amount) ?? 0m,
                ProductCode = ebLine.SellerOrderReference
            };
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Detect ebInterface version from the namespace URI in the raw XML.</summary>
    private static string? DetectVersion(string rawXml)
    {
        if (rawXml.Contains(EbInterfaceNamespaces.V6, StringComparison.OrdinalIgnoreCase))
            return "6.0";
        if (rawXml.Contains(EbInterfaceNamespaces.V5, StringComparison.OrdinalIgnoreCase))
            return "5.0";
        if (rawXml.Contains(EbInterfaceNamespaces.V4, StringComparison.OrdinalIgnoreCase))
            return "4.3";
        return null;
    }

    /// <summary>Add validation results if the detected version is non-standard or missing.</summary>
    private static void ValidateVersion(string? version, List<ValidationResult> results)
    {
        if (version is null)
        {
            results.Add(new ValidationResult(
                "EB-VER-0",
                "ebInterface namespace could not be detected. Expected one of: 4p3, 5p0, 6p0.",
                ValidationSeverity.Warning,
                "/*"));
        }
        else
        {
            results.Add(new ValidationResult(
                "EB-VER-0",
                $"ebInterface version {version} detected.",
                ValidationSeverity.Info,
                "/*",
                version));
        }
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
