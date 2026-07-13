using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Entities;

/// <summary>Webhook configuration for event notifications.</summary>
public class WebhookConfig
{
    /// <summary>Unique webhook configuration identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this webhook belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Display name for the webhook.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Target URL for webhook delivery.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 secret for payload signing.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Whether this webhook is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Event types that trigger this webhook.</summary>
    public List<WebhookEventType> Events { get; set; } = new();

    /// <summary>Content type for the webhook payload.</summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum number of delivery retry attempts.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Count of successful deliveries.</summary>
    public int? SuccessCount { get; set; }

    /// <summary>Count of failed deliveries.</summary>
    public int? FailureCount { get; set; }

    /// <summary>UTC timestamp when the webhook was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>UTC timestamp of the last triggered delivery.</summary>
    public DateTime? LastTriggeredAt { get; set; }

    /// <summary>Navigation property to the tenant.</summary>
    public Tenant Tenant { get; set; } = null!;
}
