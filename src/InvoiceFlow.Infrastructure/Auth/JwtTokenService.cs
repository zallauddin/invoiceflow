using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace InvoiceFlow.Infrastructure.Auth;

/// <summary>
/// Generates and validates JWT access tokens and refresh tokens.
/// Access tokens contain user identity claims (email, tenant, role).
/// Refresh tokens are opaque random strings stored in the database.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(Core.Entities.User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetRequiredConfig("Jwt:Key")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("tenant_id", user.TenantId.ToString()),
            new("display_name", user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expires = DateTime.UtcNow.AddMinutes(GetExpirationMinutes());

        var token = new JwtSecurityToken(
            issuer: GetRequiredConfig("Jwt:Issuer"),
            audience: GetRequiredConfig("Jwt:Audience"),
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(GetRequiredConfig("Jwt:Key"));

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = GetRequiredConfig("Jwt:Issuer"),
                ValidAudience = GetRequiredConfig("Jwt:Audience"),
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public int GetExpirationMinutes() =>
        int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var minutes) ? minutes : 60;

    private string GetRequiredConfig(string key) =>
        _configuration[key] ?? throw new InvalidOperationException($"{key} is not configured.");
}
