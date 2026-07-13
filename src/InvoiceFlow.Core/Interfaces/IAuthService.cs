using InvoiceFlow.Core.DTOs.Auth;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>Service for user authentication, registration, and token management.</summary>
public interface IAuthService
{
    /// <summary>Authenticates a user by email/password within a tenant.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Registers a new user within a tenant.</summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>Refreshes an access token using a valid refresh token.</summary>
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>Revokes a refresh token (logout).</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
