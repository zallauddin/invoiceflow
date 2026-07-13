using System.Text;
using System.Text.Json;
using InvoiceFlow.Infrastructure.AI.Extraction;

namespace InvoiceFlow.Infrastructure.AI.Extraction;

/// <summary>
/// LLM-based extraction service supporting Anthropic, OpenAI, and Google providers.
/// </summary>
public sealed class LlmExtractionService : ILlmExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string ExtractionPrompt = @"You are an expert invoice data extraction system. Extract structured data from the provided invoice text/image.

Return ONLY valid JSON with the following fields (include only fields you can confidently extract):
{
  ""InvoiceNumber"": ""string"",
  ""InvoiceDate"": ""YYYY-MM-DD"",
  ""DueDate"": ""YYYY-MM-DD"",
  ""VendorName"": ""string"",
  ""VendorTaxId"": ""string"",
  ""VendorAddress"": ""string"",
  ""CustomerName"": ""string"",
  ""CustomerTaxId"": ""string"",
  ""CustomerAddress"": ""string"",
  ""Currency"": ""ISO 4217 code (EUR, USD, etc.)"",
  ""TotalAmount"": ""decimal as string"",
  ""TaxAmount"": ""decimal as string"",
  ""SubtotalAmount"": ""decimal as string"",
  ""PaymentTerms"": ""string"",
  ""IBAN"": ""string"",
  ""BIC"": ""string"",
  ""LineItems"": [
    {
      ""Description"": ""string"",
      ""Quantity"": ""decimal as string"",
      ""UnitPrice"": ""decimal as string"",
      ""TotalPrice"": ""decimal as string"",
      ""TaxRate"": ""decimal as string""
    }
  ]
}

For each field, also provide a confidence score (0.0-1.0) in a separate ""_confidence"" object.
Example: { ""InvoiceNumber"": ""INV-123"", ""InvoiceNumber_confidence"": 0.95 }

Only include fields you can extract with confidence >= 0.7. Omit uncertain fields entirely.";

    public LlmExtractionService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task<LlmExtractionResult> ExtractFromTextAsync(
        string text,
        LlmExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new LlmExtractionOptions();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var prompt = $"{ExtractionPrompt}\n\nInvoice Text:\n{text}";
            var response = await CallLlmAsync(prompt, null, options, cancellationToken);
            
            return ParseLlmResponse(response, options, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new LlmExtractionResult
            {
                Confidence = 0,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                RawResponse = $"Error: {ex.Message}",
                Provider = options.Provider
            };
        }
    }

    /// <inheritdoc />
    public async Task<LlmExtractionResult> ExtractFromImageAsync(
        string imagePath,
        LlmExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new LlmExtractionOptions();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
            var mimeType = GetMimeType(imagePath);
            
            return await ExtractFromImageBytesAsync(imageBytes, mimeType, options, cancellationToken);
        }
        catch (Exception ex)
        {
            return new LlmExtractionResult
            {
                Confidence = 0,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                RawResponse = $"Error: {ex.Message}",
                Provider = options.Provider
            };
        }
    }

    /// <inheritdoc />
    public async Task<LlmExtractionResult> ExtractFromImageBytesAsync(
        byte[] imageBytes,
        string mimeType,
        LlmExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new LlmExtractionOptions();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var base64Image = Convert.ToBase64String(imageBytes);
            var response = await CallLlmAsync(ExtractionPrompt, base64Image, options, cancellationToken, mimeType);
            
            return ParseLlmResponse(response, options, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new LlmExtractionResult
            {
                Confidence = 0,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                RawResponse = $"Error: {ex.Message}",
                Provider = options.Provider
            };
        }
    }

    private async Task<string> CallLlmAsync(
        string prompt,
        string? base64Image,
        LlmExtractionOptions options,
        CancellationToken cancellationToken,
        string mimeType = "image/png")
    {
        return options.Provider switch
        {
            LlmProvider.Anthropic => await CallAnthropicAsync(prompt, base64Image, options, cancellationToken, mimeType),
            LlmProvider.OpenAI => await CallOpenAIAsync(prompt, base64Image, options, cancellationToken, mimeType),
            LlmProvider.Google => await CallGoogleAsync(prompt, base64Image, options, cancellationToken, mimeType),
            _ => throw new ArgumentException($"Unsupported provider: {options.Provider}")
        };
    }

    private async Task<string> CallAnthropicAsync(
        string prompt,
        string? base64Image,
        LlmExtractionOptions options,
        CancellationToken cancellationToken,
        string mimeType)
    {
        var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Anthropic API key not provided");

        var model = string.IsNullOrEmpty(options.Model) ? "claude-3-5-sonnet-20241022" : options.Model;
        var baseUrl = options.BaseUrl ?? "https://api.anthropic.com";

        var messages = new List<object>
        {
            new { role = "user", content = new List<object>() }
        };

        var userContent = (List<object>)messages[0].GetType().GetProperty("content")!.GetValue(messages[0])!;
        
        if (!string.IsNullOrEmpty(base64Image))
        {
            userContent.Add(new { type = "image", source = new { type = "base64", media_type = mimeType, data = base64Image } });
        }
        userContent.Add(new { type = "text", text = prompt });

        var requestBody = new
        {
            model = model,
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
            messages = messages
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, _jsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(responseJson);
        
        // Anthropic returns content as array of blocks
        var content = jsonDoc.RootElement.GetProperty("content");
        var textContent = content.EnumerateArray()
            .Where(c => c.GetProperty("type").GetString() == "text")
            .Select(c => c.GetProperty("text").GetString())
            .FirstOrDefault() ?? string.Empty;

        return textContent;
    }

    private async Task<string> CallOpenAIAsync(
        string prompt,
        string? base64Image,
        LlmExtractionOptions options,
        CancellationToken cancellationToken,
        string mimeType)
    {
        var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("OpenAI API key not provided");

        var model = string.IsNullOrEmpty(options.Model) ? "gpt-4o" : options.Model;
        var baseUrl = options.BaseUrl ?? "https://api.openai.com";

        var messages = new List<object>
        {
            new { role = "user", content = new List<object>() }
        };

        var userContent = (List<object>)messages[0].GetType().GetProperty("content")!.GetValue(messages[0])!;
        
        if (!string.IsNullOrEmpty(base64Image))
        {
            userContent.Add(new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } });
        }
        userContent.Add(new { type = "text", text = prompt });

        var requestBody = new
        {
            model = model,
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
            messages = messages
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, _jsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(responseJson);
        
        var content = jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return content ?? string.Empty;
    }

    private async Task<string> CallGoogleAsync(
        string prompt,
        string? base64Image,
        LlmExtractionOptions options,
        CancellationToken cancellationToken,
        string mimeType)
    {
        var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Google API key not provided");

        var model = string.IsNullOrEmpty(options.Model) ? "gemini-1.5-pro" : options.Model;
        var baseUrl = options.BaseUrl ?? "https://generativelanguage.googleapis.com";

        var parts = new List<object>();
        
        if (!string.IsNullOrEmpty(base64Image))
        {
            parts.Add(new { inline_data = new { mime_type = mimeType, data = base64Image } });
        }
        parts.Add(new { text = prompt });

        var requestBody = new
        {
            contents = new[] { new { parts = parts } },
            generationConfig = new
            {
                maxOutputTokens = options.MaxTokens,
                temperature = options.Temperature
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, _jsonOptions), Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(responseJson);
        
        var content = jsonDoc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        return content ?? string.Empty;
    }

    private LlmExtractionResult ParseLlmResponse(
        string response,
        LlmExtractionOptions options,
        long processingTimeMs)
    {
        try
        {
            // Extract JSON from response (might be wrapped in markdown code blocks)
            var jsonText = ExtractJson(response);
            
            var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var fieldConfidences = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in root.EnumerateObject())
            {
                var name = property.Name;
                var value = property.Value.GetString() ?? string.Empty;
                
                // Skip confidence fields
                if (name.EndsWith("_confidence", StringComparison.OrdinalIgnoreCase))
                {
                    var fieldName = name[..^"_confidence".Length];
                    if (double.TryParse(value, out var conf))
                    {
                        fieldConfidences[fieldName] = Math.Clamp(conf, 0.0, 1.0);
                    }
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    fields[name] = value;
                }
            }

            // Calculate overall confidence
            var overallConfidence = fieldConfidences.Values.Any() 
                ? fieldConfidences.Values.Average() 
                : 0.0;

            return new LlmExtractionResult
            {
                Fields = fields,
                Confidence = overallConfidence,
                FieldConfidences = fieldConfidences,
                RawResponse = response,
                Provider = options.Provider,
                Model = string.IsNullOrEmpty(options.Model) ? GetDefaultModel(options.Provider) : options.Model,
                ProcessingTimeMs = processingTimeMs
            };
        }
        catch (Exception)
        {
            return new LlmExtractionResult
            {
                Confidence = 0,
                RawResponse = response,
                Provider = options.Provider,
                ProcessingTimeMs = processingTimeMs
            };
        }
    }

    private static string ExtractJson(string text)
    {
        // Try to find JSON in markdown code blocks
        var codeBlockMatch = System.Text.RegularExpressions.Regex.Match(text, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        if (codeBlockMatch.Success)
            return codeBlockMatch.Groups[1].Value;

        // Try to find first { to last }
        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return text.Substring(firstBrace, lastBrace - firstBrace + 1);

        return text;
    }

    private static string GetDefaultModel(LlmProvider provider) => provider switch
    {
        LlmProvider.Anthropic => "claude-3-5-sonnet-20241022",
        LlmProvider.OpenAI => "gpt-4o",
        LlmProvider.Google => "gemini-1.5-pro",
        _ => string.Empty
    };

    private static string GetMimeType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".tiff" or ".tif" => "image/tiff",
        _ => "image/png"
    };
}