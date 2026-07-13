using System.Globalization;
using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.EbInterface.Models;

namespace InvoiceFlow.Infrastructure.Formats.EbInterface;

/// <summary>
/// Validates an ebInterface XML stream against Austrian e-invoicing business rules.
/// Checks structural XML well-formedness and ebInterface-specific mandatory fields.
/// </summary>
public sealed class EbInterfaceFormatValidator : IFormatValidator
{
    /// <summary>Standard Austrian VAT rates (0%, 10%, 13%, 20%).</summary>
    private static readonly decimal[] ValidAustrianVatRates = [0m, 10m, 13m, 20m];

    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.EbInterface;

    /// <summary>Validate ebInterface XML content against format-specific business rules.</summary>
    public async Task<FormatValidationResult> ValidateAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        var results = new List<ValidationResult>();

        // 1. Structural XML validation (well-formedness)
        ValidateXmlStructure(ms, results);
        ms.Position = 0;

        // 2. Detect namespace and determine ebInterface version
        var rawXml = await ReadRawXml(ms, ct);
        var detectedFormat = DetectFormat(rawXml);
        ms.Position = 0;

        if (detectedFormat is null)
        {
            results.Add(new ValidationResult(
                "EB-FORMAT-0",
                "Not an ebInterface document. Expected namespace: http://www.ebinterface.at/schema/6p0/, 5p0/, or 4p3/.",
                ValidationSeverity.Error,
                "/*"));
            return BuildResult(results, InvoiceFormat.EbInterface);
        }

        // 3. Deserialize for business rule validation
        EbInvoice? invoice = null;
        try
        {
            invoice = DeserializeFromStream<EbInvoice>(ms);
        }
        catch (InvalidOperationException ex)
        {
            results.Add(new ValidationResult("EB-FORMAT-1", $"XML deserialization failed: {ex.Message}", ValidationSeverity.Error));
            return BuildResult(results, InvoiceFormat.EbInterface);
        }

        if (invoice is null)
        {
            results.Add(new ValidationResult("EB-FORMAT-1", "Failed to deserialize ebInterface Invoice element.", ValidationSeverity.Error));
            return BuildResult(results, InvoiceFormat.EbInterface);
        }

        // 4. ebInterface business rule validation
        ValidateBusinessRules(invoice, results);

        return BuildResult(results, InvoiceFormat.EbInterface);
    }

    /// <summary>Validate basic XML structure (well-formedness).</summary>
    private static void ValidateXmlStructure(MemoryStream stream, List<ValidationResult> results)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.None,
                DtdProcessing = DtdProcessing.Prohibit,
            };

            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read())
            {
                // Reading through to verify well-formedness
            }
        }
        catch (XmlException ex)
        {
            results.Add(new ValidationResult(
                "EB-STRUCT-0",
                $"XML is not well-formed: {ex.Message}",
                ValidationSeverity.Error,
                null,
                $"Line {ex.LineNumber}, Position {ex.LinePosition}"));
        }
    }

    /// <summary>Read the first 4096 characters to detect the ebInterface namespace.</summary>
    private static async Task<string> ReadRawXml(MemoryStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var buffer = new char[4096];
        var bytesRead = await reader.ReadAsync(buffer, ct);
        return new string(buffer, 0, bytesRead);
    }

    /// <summary>Detect ebInterface format from namespace in raw XML header.</summary>
    private static InvoiceFormat? DetectFormat(string rawXml)
    {
        if (rawXml.Contains(EbInterfaceNamespaces.V6, StringComparison.OrdinalIgnoreCase)
            || rawXml.Contains(EbInterfaceNamespaces.V5, StringComparison.OrdinalIgnoreCase)
            || rawXml.Contains(EbInterfaceNamespaces.V4, StringComparison.OrdinalIgnoreCase))
        {
            return InvoiceFormat.EbInterface;
        }
        return null;
    }

    /// <summary>Validate ebInterface business rules on the deserialized invoice.</summary>
    private static void ValidateBusinessRules(EbInvoice invoice, List<ValidationResult> results)
    {
        // EB-1: InvoiceNumber must be present
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
        {
            results.Add(new ValidationResult("EB-1", "InvoiceNumber is mandatory.", ValidationSeverity.Error, "/Invoice/InvoiceNumber"));
        }
        else
        {
            results.Add(new ValidationResult("EB-1", "InvoiceNumber present.", ValidationSeverity.Info, "/Invoice/InvoiceNumber", invoice.InvoiceNumber));
        }

        // EB-2: InvoiceDate must be present
        if (!invoice.InvoiceDate.HasValue || invoice.InvoiceDate == default)
        {
            results.Add(new ValidationResult("EB-2", "InvoiceDate is mandatory.", ValidationSeverity.Error, "/Invoice/InvoiceDate"));
        }
        else
        {
            results.Add(new ValidationResult("EB-2", "InvoiceDate present.", ValidationSeverity.Info, "/Invoice/InvoiceDate", invoice.InvoiceDate.Value.ToString("O")));
        }

        // EB-3: Biller must have VATIdentificationNumber
        if (invoice.Biller is null)
        {
            results.Add(new ValidationResult("EB-3", "Biller element is mandatory.", ValidationSeverity.Error, "/Invoice/Biller"));
        }
        else if (string.IsNullOrWhiteSpace(invoice.Biller.VATIdentificationNumber))
        {
            results.Add(new ValidationResult("EB-3", "Biller must have a VATIdentificationNumber (UID).", ValidationSeverity.Error, "/Invoice/Biller/VATIdentificationNumber"));
        }
        else
        {
            results.Add(new ValidationResult("EB-3", "Biller VATIdentificationNumber present.", ValidationSeverity.Info, "/Invoice/Biller/VATIdentificationNumber", invoice.Biller.VATIdentificationNumber));
        }

        // EB-4: InvoiceRecipient must have valid Address
        if (invoice.InvoiceRecipient is null)
        {
            results.Add(new ValidationResult("EB-4", "InvoiceRecipient element is mandatory.", ValidationSeverity.Error, "/Invoice/InvoiceRecipient"));
        }
        else if (invoice.InvoiceRecipient.Address is null
            || string.IsNullOrWhiteSpace(invoice.InvoiceRecipient.Address.Street)
            || string.IsNullOrWhiteSpace(invoice.InvoiceRecipient.Address.Town))
        {
            results.Add(new ValidationResult("EB-4", "InvoiceRecipient must have a valid Address (Street and Town required).", ValidationSeverity.Warning, "/Invoice/InvoiceRecipient/Address"));
        }
        else
        {
            results.Add(new ValidationResult("EB-4", "InvoiceRecipient address present.", ValidationSeverity.Info, "/Invoice/InvoiceRecipient/Address"));
        }

        // EB-5: TotalGrossAmount must be positive
        if (invoice.TotalGrossAmount <= 0)
        {
            results.Add(new ValidationResult("EB-5", "TotalGrossAmount must be greater than zero.", ValidationSeverity.Error, "/Invoice/TotalGrossAmount"));
        }
        else
        {
            results.Add(new ValidationResult("EB-5", "TotalGrossAmount is positive.", ValidationSeverity.Info, "/Invoice/TotalGrossAmount", invoice.TotalGrossAmount.ToString(CultureInfo.InvariantCulture)));
        }

        // EB-6: Each LineItem must have Quantity > 0
        for (var i = 0; i < invoice.ListLineItem.Count; i++)
        {
            var lineItem = invoice.ListLineItem[i];
            if (lineItem.Quantity is null || lineItem.Quantity.Value <= 0)
            {
                results.Add(new ValidationResult("EB-6", $"LineItem {i + 1}: Quantity must be greater than zero.", ValidationSeverity.Error, $"/Invoice/ListLineItem/LineItem[{i + 1}]/Quantity"));
            }
            else
            {
                results.Add(new ValidationResult("EB-6", $"LineItem {i + 1}: Quantity is valid.", ValidationSeverity.Info, $"/Invoice/ListLineItem/LineItem[{i + 1}]/Quantity", lineItem.Quantity.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        // EB-7: Currency is EUR (Austrian e-invoicing standard)
        results.Add(new ValidationResult("EB-7", "ebInterface uses EUR as default currency.", ValidationSeverity.Info, "/Invoice", "EUR"));

        // EB-8: TaxRate must be valid Austrian rate (0, 10, 13, 20)
        for (var i = 0; i < invoice.ListLineItem.Count; i++)
        {
            var lineItem = invoice.ListLineItem[i];
            if (lineItem.Tax is not null && lineItem.Tax.TaxRate > 0)
            {
                var isValidRate = Array.Exists(ValidAustrianVatRates, r => r == lineItem.Tax.TaxRate);
                if (!isValidRate)
                {
                    results.Add(new ValidationResult("EB-8", $"LineItem {i + 1}: TaxRate {lineItem.Tax.TaxRate}% is not a standard Austrian VAT rate (expected 0, 10, 13, or 20%).", ValidationSeverity.Warning, $"/Invoice/ListLineItem/LineItem[{i + 1}]/Tax/TaxRate"));
                }
                else
                {
                    results.Add(new ValidationResult("EB-8", $"LineItem {i + 1}: TaxRate {lineItem.Tax.TaxRate}% is a valid Austrian VAT rate.", ValidationSeverity.Info, $"/Invoice/ListLineItem/LineItem[{i + 1}]/Tax/TaxRate"));
                }
            }
        }
    }

    private static FormatValidationResult BuildResult(List<ValidationResult> results, InvoiceFormat format)
    {
        var hasErrors = results.Any(r => r.Severity == ValidationSeverity.Error);
        return new FormatValidationResult(!hasErrors, results, format);
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
