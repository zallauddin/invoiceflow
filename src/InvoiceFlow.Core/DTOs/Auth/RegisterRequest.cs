using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.DTOs.Auth;

/// <summary>Request payload for user registration.</summary>
public sealed record RegisterRequest
{
    /// <summary>User's email address.</summary>
    public required string Email { get; init; }

    /// <summary>User's plaintext password.</summary>
    public required string Password { get; init; }

    /// <summary>Display name of the user.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Tenant ID to register under.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Role to assign (default: User).</summary>
    public UserRole Role { get; init; } = UserRole.User;
}
