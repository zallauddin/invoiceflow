namespace InvoiceFlow.Core.DTOs.Auth;

/// <summary>Request payload for refreshing an access token.</summary>
public sealed record RefreshTokenRequest
{
    /// <summary>The refresh token obtained during login.</summary>
    public required string RefreshToken { get; init; }
}
