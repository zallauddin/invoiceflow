using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Infrastructure.AI.Extraction;

/// <summary>
/// Self-learning template matching service that uses pattern recognition
/// to match invoices to known templates and create new ones from successful extractions.
/// </summary>
public sealed partial class TemplateMatchingService : ITemplateMatchingService
{
    private readonly ConcurrentDictionary<Guid, InvoiceTemplate> _templates = new();
    private readonly ILogger<TemplateMatchingService> _logger;
    private readonly ILlmExtractionService? _llmService;

    // Key phrases that help identify invoice layout patterns
    private static readonly Dictionary<string, string[]> LayoutPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["header_invoice_number"] = ["invoice no", "invoice #", "invoice number", "inv-", "rechnung nr"],
        ["header_dates"] = ["invoice date", "date of invoice", "rechnungsdatum", "due date", "payment due"],
        ["vendor_block"] = ["vendor", "supplier", "from", "seller", "bill from", "rechnungssteller"],
        ["customer_block"] = ["customer", "bill to", "ship to", "buyer", "rechnungsempfänger"],
        ["totals_block"] = ["total", "amount due", "balance due", "grand total", "summe"],
        ["tax_block"] = ["vat", "tax", "sales tax", "ust", "mwst"],
        ["line_items"] = ["description", "quantity", "unit price", "amount", "pos", "qty"],
        ["payment_info"] = ["payment terms", "bank transfer", "iban", "bic", "payment details"]
    };

    public TemplateMatchingService(ILogger<TemplateMatchingService> logger, ILlmExtractionService? llmService = null)
    {
        _logger = logger;
        _llmService = llmService;
    }

    /// <inheritdoc />
    public Task<TemplateMatchResult> MatchOrCreateTemplateAsync(
        string rawText,
        string? imagePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return Task.FromResult(new TemplateMatchResult { HasMatch = false, Confidence = 0 });
        }

        var normalizedText = NormalizeText(rawText);
        var features = ExtractLayoutFeatures(normalizedText);

        // Try to match against existing templates
        var bestMatch = FindBestMatch(normalizedText, features);

        if (bestMatch.HasValue)
        {
            var (template, confidence) = bestMatch.Value;

            // Use template field mappings to extract fields
            var extractedFields = ExtractFieldsUsingTemplate(normalizedText, template);
            var fieldConfidences = CalculateFieldConfidences(extractedFields, template);

            _logger.LogInformation(
                "Matched invoice to template '{TemplateName}' with confidence {Confidence:P2}",
                template.Name, confidence);

            return Task.FromResult(new TemplateMatchResult
            {
                HasMatch = true,
                Template = template,
                Confidence = confidence,
                ExtractedFields = extractedFields,
                FieldConfidences = fieldConfidences
            });
        }

        _logger.LogInformation("No matching template found for document. Will need to create new template.");

        // No match found
        return Task.FromResult(new TemplateMatchResult
        {
            HasMatch = false,
            Confidence = 0
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<InvoiceTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = _templates.Values
            .OrderByDescending(t => t.UsageCount)
            .ThenByDescending(t => t.AverageAccuracy)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<InvoiceTemplate>>(templates);
    }

    /// <inheritdoc />
    public Task<InvoiceTemplate> UpdateTemplateAsync(
        Guid templateId,
        Dictionary<string, string> extractedFields,
        double accuracy,
        CancellationToken cancellationToken = default)
    {
        if (!_templates.TryGetValue(templateId, out var existing))
        {
            throw new InvalidOperationException($"Template {templateId} not found.");
        }

        var newUsageCount = existing.UsageCount + 1;
        var newAverageAccuracy = ((existing.AverageAccuracy * existing.UsageCount) + accuracy) / newUsageCount;

        var updatedMappings = LearnFieldMappings(existing.FieldMappings, extractedFields);

        var updatedFeatures = new Dictionary<string, string>(existing.VisualFeatures);
        UpdateVisualFeatures(updatedFeatures, extractedFields);

        var updated = existing with
        {
            FieldMappings = updatedMappings,
            VisualFeatures = updatedFeatures,
            UsageCount = newUsageCount,
            AverageAccuracy = newAverageAccuracy,
            ConfidenceThreshold = Math.Max(0.5, existing.ConfidenceThreshold - 0.01),
            UpdatedAt = DateTime.UtcNow
        };

        _templates[templateId] = updated;

        _logger.LogInformation(
            "Updated template '{TemplateName}': usage={UsageCount}, accuracy={Accuracy:P2}",
            updated.Name, updated.UsageCount, updated.AverageAccuracy);

        return Task.FromResult(updated);
    }

    /// <inheritdoc />
    public Task<InvoiceTemplate> CreateTemplateAsync(
        string name,
        string rawText,
        Dictionary<string, string> extractedFields,
        CancellationToken cancellationToken = default)
    {
        var normalizedText = NormalizeText(rawText);
        var features = ExtractLayoutFeatures(normalizedText);

        var fieldMappings = GenerateFieldMappings(extractedFields, normalizedText);

        var template = new InvoiceTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Auto-generated template from invoice extraction",
            VisualFeatures = features,
            FieldMappings = fieldMappings,
            ExampleText = rawText.Length > 500 ? rawText[..500] : rawText,
            UsageCount = 1,
            AverageAccuracy = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _templates[template.Id] = template;

        _logger.LogInformation(
            "Created new template '{TemplateName}' with {FieldCount} field mappings",
            template.Name, template.FieldMappings.Count);

        return Task.FromResult(template);
    }

    /// <inheritdoc />
    public Task DeleteTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        if (!_templates.TryRemove(templateId, out var removed))
        {
            _logger.LogWarning("Template {TemplateId} not found for deletion", templateId);
        }
        else
        {
            _logger.LogInformation("Deleted template '{TemplateName}'", removed.Name);
        }

        return Task.CompletedTask;
    }

    #region Private Methods - Pattern Matching

    private static string NormalizeText(string text)
    {
        var normalized = text.ToLowerInvariant();
        normalized = WhitespaceRegex().Replace(normalized, " ");
        return normalized.Trim();
    }

    private static Dictionary<string, string> ExtractLayoutFeatures(string normalizedText)
    {
        var features = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (featureKey, keywords) in LayoutPatterns)
        {
            var found = keywords.Any(kw => normalizedText.Contains(kw, StringComparison.Ordinal));
            features[featureKey] = found ? "present" : "absent";
        }

        features["has_line_items"] = DetectLineItemStructure(normalizedText);
        features["line_count"] = normalizedText.Split('\n').Length.ToString();
        features["total_amount_position"] = DetectTotalAmountPosition(normalizedText);

        return features;
    }

    private (InvoiceTemplate Template, double Confidence)? FindBestMatch(
        string normalizedText,
        Dictionary<string, string> features)
    {
        if (_templates.IsEmpty)
            return null;

        InvoiceTemplate? bestTemplate = null;
        double bestScore = 0;

        foreach (var template in _templates.Values)
        {
            var score = CalculateMatchScore(normalizedText, features, template);

            if (score >= template.ConfidenceThreshold && score > bestScore)
            {
                bestTemplate = template;
                bestScore = score;
            }
        }

        return bestTemplate is not null ? (bestTemplate, bestScore) : null;
    }

    private static double CalculateMatchScore(
        string normalizedText,
        Dictionary<string, string> features,
        InvoiceTemplate template)
    {
        var scores = new List<double>();

        // Feature match score (40% weight)
        var featureMatchCount = 0;
        var featureTotal = 0;
        foreach (var (key, templateValue) in template.VisualFeatures)
        {
            featureTotal++;
            if (features.TryGetValue(key, out var textValue) && textValue == templateValue)
            {
                featureMatchCount++;
            }
        }
        var featureScore = featureTotal > 0 ? (double)featureMatchCount / featureTotal : 0;
        scores.Add(featureScore * 0.4);

        // Regex pattern match score (30% weight)
        var regexScore = CalculateRegexMatchScore(normalizedText, template);
        scores.Add(regexScore * 0.3);

        // Key phrase overlap score (30% weight)
        var phraseScore = CalculatePhraseOverlapScore(normalizedText, template);
        scores.Add(phraseScore * 0.3);

        return scores.Sum();
    }

    private static double CalculateRegexMatchScore(string normalizedText, InvoiceTemplate template)
    {
        if (template.FieldMappings.Count == 0)
            return 0;

        var matched = 0;
        var total = 0;

        foreach (var (_, mapping) in template.FieldMappings)
        {
            if (string.IsNullOrEmpty(mapping.RegexPattern))
                continue;

            total++;
            try
            {
                if (Regex.IsMatch(normalizedText, mapping.RegexPattern, RegexOptions.IgnoreCase))
                {
                    matched++;
                }
            }
            catch (RegexParseException)
            {
                // Skip invalid patterns
            }
        }

        return total > 0 ? (double)matched / total : 0.5;
    }

    private static double CalculatePhraseOverlapScore(string normalizedText, InvoiceTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.ExampleText))
            return 0.5;

        var exampleWords = template.ExampleText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(100)
            .ToHashSet(StringComparer.Ordinal);

        var textWords = normalizedText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(200)
            .ToHashSet(StringComparer.Ordinal);

        if (exampleWords.Count == 0 || textWords.Count == 0)
            return 0;

        var intersection = exampleWords.Intersect(textWords).Count();
        var union = exampleWords.Union(textWords).Count();

        return union > 0 ? (double)intersection / union : 0;
    }

    #endregion

    #region Private Methods - Field Extraction

    private static Dictionary<string, string> ExtractFieldsUsingTemplate(
        string normalizedText,
        InvoiceTemplate template)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, mapping) in template.FieldMappings)
        {
            if (!string.IsNullOrEmpty(mapping.RegexPattern))
            {
                try
                {
                    var match = Regex.Match(normalizedText, mapping.RegexPattern, RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        fields[fieldName] = match.Groups[1].Value.Trim();
                        continue;
                    }
                    else if (match.Success)
                    {
                        fields[fieldName] = match.Value.Trim();
                        continue;
                    }
                }
                catch (RegexParseException)
                {
                    // Fall through
                }
            }

            if (!string.IsNullOrEmpty(mapping.ExtractionPrompt))
            {
                var extractedValue = ExtractByKeywordProximity(normalizedText, mapping.ExtractionPrompt);
                if (!string.IsNullOrEmpty(extractedValue))
                {
                    fields[fieldName] = extractedValue;
                }
            }
        }

        return fields;
    }

    private static string ExtractByKeywordProximity(string text, string keyword)
    {
        var normalizedKeyword = keyword.ToLowerInvariant();
        var normalizedText = text.ToLowerInvariant();
        var idx = normalizedText.IndexOf(normalizedKeyword, StringComparison.Ordinal);

        if (idx < 0)
            return string.Empty;

        var afterKeyword = text[(idx + keyword.Length)..];
        var lineEnd = afterKeyword.IndexOf('\n');
        var lineContent = lineEnd >= 0 ? afterKeyword[..lineEnd] : afterKeyword;

        var value = lineContent
            .TrimStart(':', ' ', '\t', '-')
            .TrimEnd();

        var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private static Dictionary<string, double> CalculateFieldConfidences(
        Dictionary<string, string> extractedFields,
        InvoiceTemplate template)
    {
        var confidences = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, value) in extractedFields)
        {
            double confidence;

            if (template.FieldMappings.TryGetValue(fieldName, out var mapping))
            {
                if (!string.IsNullOrEmpty(mapping.RegexPattern))
                {
                    try
                    {
                        if (Regex.IsMatch(value, mapping.RegexPattern, RegexOptions.IgnoreCase))
                        {
                            confidence = 0.9;
                        }
                        else
                        {
                            confidence = 0.6;
                        }
                    }
                    catch (RegexParseException)
                    {
                        confidence = 0.5;
                    }
                }
                else
                {
                    confidence = string.IsNullOrWhiteSpace(value) ? 0.0 : 0.7;
                }
            }
            else
            {
                confidence = 0.5;
            }

            confidences[fieldName] = confidence;
        }

        return confidences;
    }

    #endregion

    #region Private Methods - Learning

    private static Dictionary<string, FieldMapping> LearnFieldMappings(
        Dictionary<string, FieldMapping> existingMappings,
        Dictionary<string, string> newFields)
    {
        var updatedMappings = new Dictionary<string, FieldMapping>(existingMappings, StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, value) in newFields)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (updatedMappings.TryGetValue(fieldName, out var existing))
            {
                updatedMappings[fieldName] = existing with
                {
                    ExtractionPrompt = !string.IsNullOrEmpty(existing.ExtractionPrompt)
                        ? existing.ExtractionPrompt
                        : fieldName,
                    IsRequired = existing.IsRequired || !string.IsNullOrWhiteSpace(value)
                };
            }
            else
            {
                var generatedPattern = GenerateRegexForField(fieldName, value);
                updatedMappings[fieldName] = new FieldMapping
                {
                    FieldName = fieldName,
                    ExtractionPrompt = fieldName,
                    RegexPattern = generatedPattern,
                    IsRequired = false,
                    DataType = InferDataType(value)
                };
            }
        }

        return updatedMappings;
    }

    private static string? GenerateRegexForField(string fieldName, string exampleValue)
    {
        if (fieldName.Contains("amount", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("total", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("price", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("subtotal", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("tax", StringComparison.OrdinalIgnoreCase))
        {
            return @"[\d\.,]+";
        }

        if (fieldName.Contains("date", StringComparison.OrdinalIgnoreCase))
        {
            return @"\d{1,2}[\.\/\-]\d{1,2}[\.\/\-]\d{2,4}";
        }

        if (fieldName.Contains("number", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("invoice", StringComparison.OrdinalIgnoreCase))
        {
            var escaped = Regex.Escape(exampleValue);
            return escaped.Length > 3 ? escaped : null;
        }

        if (fieldName.Contains("taxid", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("vat", StringComparison.OrdinalIgnoreCase))
        {
            return @"[A-Z]{2}[\d\w\-]+|\d{6,12}";
        }

        return null;
    }

    private static string InferDataType(string value)
    {
        if (decimal.TryParse(value, out _))
            return "decimal";
        if (DateTime.TryParse(value, out _))
            return "date";
        if (bool.TryParse(value, out _))
            return "boolean";
        return "string";
    }

    private static Dictionary<string, FieldMapping> GenerateFieldMappings(
        Dictionary<string, string> extractedFields,
        string normalizedText)
    {
        var mappings = new Dictionary<string, FieldMapping>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, value) in extractedFields)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var pattern = GenerateRegexForField(fieldName, value);

            mappings[fieldName] = new FieldMapping
            {
                FieldName = fieldName,
                ExtractionPrompt = fieldName,
                RegexPattern = pattern,
                IsRequired = IsCommonRequiredField(fieldName),
                DataType = InferDataType(value)
            };
        }

        return mappings;
    }

    private static bool IsCommonRequiredField(string fieldName)
    {
        var required = new[]
        {
            "InvoiceNumber", "InvoiceDate", "VendorName", "TotalAmount",
            "Currency", "Subtotal", "TaxAmount"
        };

        return required.Any(r => fieldName.Contains(r, StringComparison.OrdinalIgnoreCase));
    }

    private static void UpdateVisualFeatures(
        Dictionary<string, string> features,
        Dictionary<string, string> extractedFields)
    {
        if (extractedFields.ContainsKey("LineItems") || extractedFields.ContainsKey("Description"))
        {
            features["has_line_items"] = "present";
        }
    }

    #endregion

    #region Private Methods - Feature Detection Helpers

    private static string DetectLineItemStructure(string text)
    {
        var hasTabs = text.Contains('\t');
        var hasPipeSeparators = text.Contains(" | ");
        var hasNumberedItems = NumberedItemRegex().IsMatch(text);
        var hasQuantityPrice = text.Contains("qty") || text.Contains("quantity") || text.Contains("unit price");

        return (hasTabs || hasPipeSeparators || hasNumberedItems || hasQuantityPrice) ? "structured" : "unstructured";
    }

    private static string DetectTotalAmountPosition(string text)
    {
        var totalIdx = text.LastIndexOf("total", StringComparison.Ordinal);
        if (totalIdx < 0) return "unknown";

        var linesBeforeTotal = text[..totalIdx].Count(c => c == '\n');
        var totalLines = text.Split('\n').Length;

        if (linesBeforeTotal > totalLines * 0.7) return "bottom";
        if (linesBeforeTotal < totalLines * 0.3) return "top";
        return "middle";
    }

    #endregion

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^\d+[\.\)\s]", RegexOptions.Multiline)]
    private static partial Regex NumberedItemRegex();
}
