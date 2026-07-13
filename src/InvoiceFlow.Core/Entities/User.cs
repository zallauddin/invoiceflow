using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Entities;

/// <summary>Represents a user within a tenant organization.</summary>
public class User
{
    /// <summary>Unique user identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this user belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Email address (used as login credential).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name of the user.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>BCrypt password hash.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Role within the tenant (Admin, User, Viewer).</summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>Whether this user account is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the user was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last successful login.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Navigation property to the tenant.</summary>
    public Tenant Tenant { get; set; } = null!;
}
