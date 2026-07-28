using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Infrastructure.Services;

/// <summary>
/// Webhook delivery service with exponential backoff retry logic.
/// Replaces simple linear retry with exponential backoff + jitter.
/// Signed payloads via HMAC-SHA256 for verification by the receiving service.
/// </summary>
public interface IWebhookDeliveryService
{
    /// <summary>Delivers a webhook payload with exponential backoff retry.</summary>
    Task<WebhookDeliveryResult> DeliverAsync(
        WebhookConfig webhook,
        object payload,
        CancellationToken cancellationToken = default);
}

public record WebhookDeliveryResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int Attempts { get; init; }
    public long DurationMs { get; init; }
}

public sealed class WebhookDeliveryService : IWebhookDeliveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryService> _logger;
    private static readonly Random Jitter = new();

    // Exponential backoff: attempt 1 = 2s, 2 = 4s, 3 = 8s, 4 = 16s, 5 = 32s
    private static readonly int[] BackoffDelaysSeconds = [1, 2, 4, 8, 16, 32];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public WebhookDeliveryService(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDeliveryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<WebhookDeliveryResult> DeliverAsync(
        WebhookConfig webhook,
        object payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhook);
        ArgumentNullException.ThrowIfNull(payload);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var maxAttempts = Math.Max(1, Math.Min(webhook.MaxRetries, BackoffDelaysSeconds.Length));
        var lastResult = new WebhookDeliveryResult { Success = false };

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var client = _httpClientFactory.CreateClient("WebhookDelivery");
                var timeoutSeconds = Math.Max(5, webhook.TimeoutSeconds);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                var json = JsonSerializer.Serialize(payload, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, webhook.ContentType ?? "application/json");

                // Sign payload with HMAC-SHA256 if secret is configured
                if (!string.IsNullOrEmpty(webhook.Secret))
                {
                    var signature = ComputeHmacSignature(json, webhook.Secret);
                    content.Headers.Add("X-InvoiceFlow-Signature", $"sha256={signature}");
                    content.Headers.Add("X-InvoiceFlow-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                }

                using var response = await client.PostAsync(webhook.Url, content, cancellationToken);

                var statusCode = (int)response.StatusCode;
                var isSuccess = statusCode >= 200 && statusCode < 300;

                _logger.LogDebug(
                    "Webhook delivery attempt {Attempt}/{MaxAttempts} to {Url} returned {StatusCode}",
                    attempt, maxAttempts, webhook.Url, statusCode);

                if (isSuccess || statusCode == 410 /* Gone — permanent failure */)
                {
                    sw.Stop();
                    return new WebhookDeliveryResult
                    {
                        Success = isSuccess,
                        StatusCode = statusCode,
                        Attempts = attempt,
                        DurationMs = sw.ElapsedMilliseconds
                    };
                }

                lastResult = new WebhookDeliveryResult
                {
                    Success = false,
                    StatusCode = statusCode,
                    ErrorMessage = $"HTTP {statusCode}",
                    Attempts = attempt
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "Webhook delivery attempt {Attempt}/{MaxAttempts} to {Url} failed: {Message}",
                    attempt, maxAttempts, webhook.Url, ex.Message);

                lastResult = new WebhookDeliveryResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Attempts = attempt
                };
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Webhook delivery attempt {Attempt}/{MaxAttempts} to {Url} timed out after {Timeout}s",
                    attempt, maxAttempts, webhook.Url, webhook.TimeoutSeconds);

                lastResult = new WebhookDeliveryResult
                {
                    Success = false,
                    ErrorMessage = "Request timed out",
                    Attempts = attempt
                };
            }

            // Don't delay on the last attempt
            if (attempt < maxAttempts)
            {
                var delay = BackoffDelaysSeconds[attempt - 1];
                var jitterMs = Jitter.Next(0, 1000);
                await Task.Delay(TimeSpan.FromSeconds(delay) + TimeSpan.FromMilliseconds(jitterMs), cancellationToken);
            }
        }

        sw.Stop();
        _logger.LogWarning(
            "Webhook delivery to {Url} failed after {Attempts} attempts ({DurationMs}ms)",
            webhook.Url, maxAttempts, sw.ElapsedMilliseconds);

        return lastResult with { DurationMs = sw.ElapsedMilliseconds };
    }

    private static string ComputeHmacSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
