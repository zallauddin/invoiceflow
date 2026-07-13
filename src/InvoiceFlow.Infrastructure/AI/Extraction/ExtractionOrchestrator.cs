using System.Diagnostics;
using System.Text.Json;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Infrastructure.AI.Extraction;

/// <summary>
/// Multi-stage extraction orchestrator that tries OCR first, then falls back to
/// LLM extraction and finally template matching when confidence is low.
/// </summary>
public sealed class ExtractionOrchestrator : IExtractionOrchestrator
{
    private readonly IOcrExtractionService _ocrService;
    private readonly ILlmExtractionService _llmService;
    private readonly ITemplateMatchingService _templateService;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    private const double ConfidenceThreshold = 0.85;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ExtractionOrchestrator(
        IOcrExtractionService ocrService,
        ILlmExtractionService llmService,
        ITemplateMatchingService templateService,
        ILogger<ExtractionOrchestrator> logger)
    {
        _ocrService = ocrService;
        _llmService = llmService;
        _templateService = templateService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExtractionOrchestratorResult> ProcessAsync(
        Document document,
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var domainEvents = new List<IDomainEvent>();

        try
        {
            _logger.LogInformation(
                "Starting extraction pipeline for document {DocumentId} (type: {MimeType})",
                document.Id, document.MimeType);

            Dictionary<string, string>? bestFields = null;
            double bestConfidence = 0;
            ExtractionMethod bestMethod = ExtractionMethod.Ocr;
            string? rawText = null;

            // Stage 1: OCR extraction
            var ocrResult = await RunOcrAsync(document, cancellationToken);
            if (ocrResult is not null)
            {
                rawText = ocrResult.RawText;
                document.OcrText = rawText;
                bestFields = ocrResult.Fields;
                bestConfidence = ocrResult.Confidence;
                bestMethod = ExtractionMethod.Ocr;

                _logger.LogInformation(
                    "OCR stage completed: confidence={Confidence:F2}, fields={FieldCount}",
                    bestConfidence, bestFields.Count);
            }

            // Stage 2: LLM fallback if confidence is below threshold
            if (bestConfidence < ConfidenceThreshold && !string.IsNullOrWhiteSpace(rawText))
            {
                var llmResult = await RunLlmFallbackAsync(rawText, cancellationToken);
                if (llmResult is not null && llmResult.Confidence > bestConfidence)
                {
                    bestFields = MergeFields(bestFields ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), llmResult.Fields);
                    bestConfidence = llmResult.Confidence;
                    bestMethod = ExtractionMethod.Llm;

                    _logger.LogInformation(
                        "LLM fallback improved confidence to {Confidence:F2}",
                        bestConfidence);
                }
            }

            // Stage 3: Template matching if still below threshold
            if (bestConfidence < ConfidenceThreshold && !string.IsNullOrWhiteSpace(rawText))
            {
                var templateResult = await RunTemplateMatchAsync(rawText, document.Id, cancellationToken);
                if (templateResult is not null && templateResult.Confidence > bestConfidence)
                {
                    bestFields = MergeFields(bestFields ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), templateResult.ExtractedFields);
                    bestConfidence = templateResult.Confidence;
                    bestMethod = ExtractionMethod.TemplateAi;

                    _logger.LogInformation(
                        "Template matching improved confidence to {Confidence:F2}",
                        bestConfidence);
                }
            }

            if (bestFields is null || bestFields.Count == 0)
            {
                _logger.LogWarning("All extraction stages failed for document {DocumentId}", document.Id);
                return ExtractionOrchestratorResult.Fail(
                    "No fields could be extracted from the document",
                    stopwatch.ElapsedMilliseconds);
            }

            // Populate invoice from extracted fields
            MapFieldsToInvoice(bestFields, invoice);

            // Extract and create line items
            invoice.Lines = CreateInvoiceLines(bestFields, invoice.Id);

            // Calculate totals from line items when available
            CalculateTotals(invoice, invoice.Lines);

            // Set extraction metadata
            invoice.ExtractionMethod = bestMethod;
            invoice.OcrConfidence = bestConfidence;
            invoice.ExtractedAt = DateTime.UtcNow;
            invoice.Status = InvoiceStatus.Extracted;
            invoice.UpdatedAt = DateTime.UtcNow;

            // Raise domain event
            var extractedEvent = new InvoiceExtractedEvent
            {
                InvoiceId = invoice.Id,
                TenantId = invoice.TenantId,
                ExtractionMethod = bestMethod.ToString(),
                Confidence = bestConfidence
            };
            domainEvents.Add(extractedEvent);

            _logger.LogInformation(
                "Extraction pipeline completed for invoice {InvoiceId}: method={Method}, confidence={Confidence:F2}, lines={LineCount}",
                invoice.Id, bestMethod, bestConfidence, invoice.Lines.Count);

            return new ExtractionOrchestratorResult
            {
                Success = true,
                ExtractionMethod = bestMethod,
                Confidence = bestConfidence,
                Invoice = invoice,
                DomainEvents = domainEvents,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction pipeline failed for document {DocumentId}", document.Id);
            return ExtractionOrchestratorResult.Fail(ex.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<OcrExtractionResult?> RunOcrAsync(Document document, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.StoragePath))
        {
            _logger.LogDebug("Skipping OCR: no storage path for document {DocumentId}", document.Id);
            return null;
        }

        try
        {
            if (IsPdf(document.MimeType))
            {
                return await _ocrService.ExtractFromPdfAsync(
                    document.StoragePath, cancellationToken: cancellationToken);
            }

            return await _ocrService.ExtractFromImageAsync(
                document.StoragePath, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR extraction failed for document {DocumentId}", document.Id);
            return null;
        }
    }

    private async Task<LlmExtractionResult?> RunLlmFallbackAsync(string rawText, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _llmService.ExtractFromTextAsync(
                rawText, cancellationToken: cancellationToken);

            return result.Success ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM fallback extraction failed");
            return null;
        }
    }

    private async Task<TemplateMatchResult?> RunTemplateMatchAsync(
        string rawText, Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _templateService.MatchOrCreateTemplateAsync(
                rawText, cancellationToken: cancellationToken);

            return result.HasMatch ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Template matching failed for document {DocumentId}", documentId);
            return null;
        }
    }

    private static void MapFieldsToInvoice(Dictionary<string, string> fields, Invoice invoice)
    {
        invoice.InvoiceNumber = GetField(fields, "InvoiceNumber") ?? invoice.InvoiceNumber;
        invoice.VendorName = GetField(fields, "VendorName") ?? invoice.VendorName;
        invoice.VendorTaxId = GetField(fields, "VendorTaxId") ?? invoice.VendorTaxId;
        invoice.BuyerName = GetField(fields, "CustomerName", "BuyerName") ?? invoice.BuyerName;
        invoice.BuyerTaxId = GetField(fields, "CustomerTaxId", "BuyerTaxId") ?? invoice.BuyerTaxId;
        invoice.Currency = GetField(fields, "Currency") ?? invoice.Currency;

        var invoiceDateStr = GetField(fields, "InvoiceDate", "Date");
        if (invoiceDateStr is not null && TryParseDate(invoiceDateStr, out var invoiceDate))
        {
            invoice.InvoiceDate = invoiceDate;
        }

        var dueDateStr = GetField(fields, "DueDate", "PaymentDueDate");
        if (dueDateStr is not null && TryParseDate(dueDateStr, out var dueDate))
        {
            invoice.DueDate = dueDate;
        }

        var totalStr = GetField(fields, "TotalAmount", "Total");
        if (totalStr is not null && TryParseDecimal(totalStr, out var totalAmount))
        {
            invoice.TotalAmount = totalAmount;
        }

        var taxStr = GetField(fields, "TaxAmount", "Tax");
        if (taxStr is not null && TryParseDecimal(taxStr, out var taxAmount))
        {
            invoice.TaxAmount = taxAmount;
        }

        var subtotalStr = GetField(fields, "SubtotalAmount", "Subtotal");
        if (subtotalStr is not null && TryParseDecimal(subtotalStr, out var subtotal))
        {
            invoice.Subtotal = subtotal;
        }
    }

    private static List<InvoiceLine> CreateInvoiceLines(Dictionary<string, string> fields, Guid invoiceId)
    {
        var lines = new List<InvoiceLine>();

        // Try to parse structured line items from JSON
        var lineItemsJson = GetField(fields, "LineItems");
        if (!string.IsNullOrWhiteSpace(lineItemsJson))
        {
            lines = ParseLineItemsJson(lineItemsJson, invoiceId);
            if (lines.Count > 0)
            {
                return lines;
            }
        }

        // Fall back to individual line item fields (Description, Quantity, UnitPrice, LineTotal)
        var description = GetField(fields, "Description", "ItemDescription");
        if (description is not null)
        {
            var line = CreateSingleLineFromFields(fields, invoiceId, lineNumber: 1);
            lines.Add(line);
        }

        return lines;
    }

    private static List<InvoiceLine> ParseLineItemsJson(string json, Guid invoiceId)
    {
        try
        {
            // The LLM may wrap LineItems in a JSON array string
            var itemsJson = json.Trim();

            // Handle case where value is a JSON array encoded as a string
            if (itemsJson.StartsWith("\"") && itemsJson.EndsWith("\""))
            {
                itemsJson = JsonSerializer.Deserialize<string>(itemsJson) ?? itemsJson;
            }

            var items = JsonSerializer.Deserialize<List<JsonElement>>(itemsJson, JsonOptions);
            if (items is null)
            {
                return [];
            }

            var lines = new List<InvoiceLine>();
            var lineNumber = 1;

            foreach (var item in items)
            {
                var description = GetStringProperty(item, "Description", "ItemDescription") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                var quantity = GetDecimalProperty(item, "Quantity", "Qty") ?? 1m;
                var unitPrice = GetDecimalProperty(item, "UnitPrice", "Price") ?? 0m;
                var lineTotal = GetDecimalProperty(item, "TotalPrice", "LineTotal", "Amount") ?? quantity * unitPrice;
                var taxRate = GetDecimalProperty(item, "TaxRate") ?? 0m;

                var taxAmount = lineTotal * taxRate / 100m;

                lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    LineNumber = lineNumber++,
                    Description = description,
                    ProductCode = GetStringProperty(item, "ProductCode", "ItemCode"),
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal,
                    TaxRate = taxRate,
                    TaxAmount = taxAmount
                });
            }

            return lines;
        }
        catch (JsonException)
        {
            // JSON parsing failed — cannot parse line items
            return [];
        }
    }

    private static InvoiceLine CreateSingleLineFromFields(Dictionary<string, string> fields, Guid invoiceId, int lineNumber)
    {
        var description = GetField(fields, "Description", "ItemDescription") ?? string.Empty;
        var quantity = GetDecimalField(fields, "Quantity", "Qty") ?? 1m;
        var unitPrice = GetDecimalField(fields, "UnitPrice", "Price") ?? 0m;
        var lineTotal = GetDecimalField(fields, "LineTotal", "Amount") ?? quantity * unitPrice;
        var taxRate = GetDecimalField(fields, "TaxRate") ?? 0m;

        return new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            LineNumber = lineNumber,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = lineTotal,
            TaxRate = taxRate,
            TaxAmount = lineTotal * taxRate / 100m
        };
    }

    private static void CalculateTotals(Invoice invoice, List<InvoiceLine> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var computedSubtotal = lines.Sum(l => l.LineTotal);
        var computedTax = lines.Sum(l => l.TaxAmount);

        // Only override if the extracted values were not already set (non-zero)
        if (invoice.Subtotal == 0)
        {
            invoice.Subtotal = computedSubtotal;
        }

        if (invoice.TaxAmount == 0)
        {
            invoice.TaxAmount = computedTax;
        }

        // Always recalculate total from subtotal + tax to ensure consistency
        invoice.TotalAmount = invoice.Subtotal + invoice.TaxAmount;
    }

    private static Dictionary<string, string> MergeFields(
        Dictionary<string, string> existing,
        Dictionary<string, string> incoming)
    {
        foreach (var (key, value) in incoming)
        {
            if (!string.IsNullOrWhiteSpace(value) && !existing.ContainsKey(key))
            {
                existing[key] = value;
            }
        }

        return existing;
    }

    private static string? GetField(Dictionary<string, string> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static decimal? GetDecimalField(Dictionary<string, string> fields, params string[] keys)
    {
        var value = GetField(fields, keys);
        return value is not null && TryParseDecimal(value, out var result) ? result : null;
    }

    private static string? GetStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static decimal? GetDecimalProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (!element.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var numericValue))
            {
                return numericValue;
            }

            if (prop.ValueKind == JsonValueKind.String && TryParseDecimal(prop.GetString() ?? string.Empty, out var stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        // Strip currency symbols and whitespace
        var cleaned = value
            .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
            .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
            .Replace("GBP", "", StringComparison.OrdinalIgnoreCase)
            .Replace("\u20ac", "")
            .Replace("$", "")
            .Replace("\u00a3", "")
            .Replace("\u00a5", "")
            .Trim();

        // Try invariant (period decimal) first
        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        // Try comma-as-decimal (European format: 1.234,56)
        var european = cleaned.Replace(".", "").Replace(",", ".");
        if (decimal.TryParse(european, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseDate(string value, out DateTime result)
    {
        // Strip time portions if present (e.g., "2024-01-15T00:00:00")
        var dateOnly = value.Contains('T') ? value[..value.IndexOf('T')] : value;

        if (DateTime.TryParse(dateOnly, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result))
        {
            return true;
        }

        // Try common European date formats
        string[] formats = ["dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "MM/dd/yyyy"];
        if (DateTime.TryParseExact(dateOnly, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result))
        {
            return true;
        }

        return false;
    }

    private static bool IsPdf(string mimeType) =>
        mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
}
