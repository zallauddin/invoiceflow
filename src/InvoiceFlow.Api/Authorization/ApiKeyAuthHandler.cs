using System.Security.Claims;
using System.Text.Encodings.Web;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Api.Authorization;

/// <summary>
/// API key authentication handler for third-party integrations.
/// Validates API keys stored in the database and creates an authenticated principal
/// with the tenant ID and a synthetic "apikey" user identity.
///
/// Usage: Add [Authorize(AuthenticationSchemes = "ApiKey")] to endpoints
/// that should accept API key authentication.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IRepository<ApiKey> _apiKeyRepository;

    public ApiKeyAuthenticationHandler(
        IRepository<ApiKey> apiKeyRepository,
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Extract API key from header or query parameter
        string? apiKey = null;

        if (Request.Headers.TryGetValue("X-API-Key", out var headerValues))
        {
            apiKey = headerValues.FirstOrDefault();
        }

        if (string.IsNullOrEmpty(apiKey) &&
            Request.Query.TryGetValue("api_key", out var queryValues))
        {
            apiKey = queryValues.FirstOrDefault();
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        // Look up the API key in the database
        // In production, use a cached lookup with Redis for performance
        var keys = await _apiKeyRepository.GetAllAsync(0, 1000);
        var storedKey = keys.FirstOrDefault(k => k.Key == apiKey && k.IsActive);

        if (storedKey is null)
        {
            return AuthenticateResult.Fail("Invalid or inactive API key.");
        }

        // Update last used timestamp
        storedKey.LastUsedAt = DateTime.UtcNow;
        await _apiKeyRepository.UpdateAsync(storedKey);

        // Create claims identity
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, storedKey.Id.ToString()),
            new Claim("tenant_id", storedKey.TenantId.ToString()),
            new Claim("api_key_name", storedKey.Name),
            new Claim("auth_method", "api_key"),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

/// <summary>
/// Options for API key authentication.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}
