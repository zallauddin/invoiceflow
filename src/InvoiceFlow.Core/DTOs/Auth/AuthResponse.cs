namespace InvoiceFlow.Core.DTOs.Auth;

/// <summary>Authentication response containing tokens and user info.</summary>
public sealed record AuthResponse
{
    /// <summary>JWT access token.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Refresh token for obtaining new access tokens.</summary>
    public required string RefreshToken { get; init; }

    /// <summary>UTC expiration time of the access token.</summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>Authenticated user information.</summary>
    public required UserDto User { get; init; }
}
