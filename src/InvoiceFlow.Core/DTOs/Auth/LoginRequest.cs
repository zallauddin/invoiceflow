namespace InvoiceFlow.Core.DTOs.Auth;

/// <summary>Request payload for user login.</summary>
public sealed record LoginRequest
{
    /// <summary>User's email address.</summary>
    public required string Email { get; init; }

    /// <summary>User's plaintext password.</summary>
    public required string Password { get; init; }

    /// <summary>Tenant slug or ID to authenticate against.</summary>
    public required string TenantSlug { get; init; }
}
