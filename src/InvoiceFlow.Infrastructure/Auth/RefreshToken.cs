namespace InvoiceFlow.Infrastructure.Auth;

/// <summary>Opaque refresh token entity stored in the database.</summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    /// <summary>The opaque refresh token string (base64).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>User this refresh token belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>Tenant this token is scoped to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>UTC expiration time.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC when the token was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC when the token was revoked (null if still active).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Whether this token has been revoked.</summary>
    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>Whether this token is expired.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>Whether this token can be used (not revoked and not expired).</summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation
    public Core.Entities.User User { get; set; } = null!;
}
