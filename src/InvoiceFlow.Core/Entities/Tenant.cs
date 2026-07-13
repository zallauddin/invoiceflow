namespace InvoiceFlow.Core.Entities;

/// <summary>Represents a tenant organization in the multi-tenant system.</summary>
public class Tenant
{
    /// <summary>Unique tenant identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the tenant organization.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe slug used in routing and API paths.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Tax identification number for the tenant.</summary>
    public string? TaxId { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string? Country { get; set; }

    /// <summary>Whether this tenant is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the tenant was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last update.</summary>
    public DateTime? UpdatedAt { get; set; }
}
