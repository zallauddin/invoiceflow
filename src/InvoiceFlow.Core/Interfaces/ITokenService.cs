using System.Security.Claims;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>Token generation and validation service.</summary>
public interface ITokenService
{
    /// <summary>Generates a JWT access token for the given user.</summary>
    string GenerateAccessToken(Entities.User user);

    /// <summary>Generates a cryptographically random refresh token string.</summary>
    string GenerateRefreshToken();

    /// <summary>Validates a JWT access token and returns the claims principal, or null if invalid.</summary>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>Returns the access token expiration in minutes.</summary>
    int GetExpirationMinutes();
}
