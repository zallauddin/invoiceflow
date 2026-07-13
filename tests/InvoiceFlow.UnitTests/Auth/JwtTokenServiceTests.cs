using System.Security.Claims;
using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace InvoiceFlow.UnitTests.Auth;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _tokenService;

    public JwtTokenServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "ThisIsATestKeyThatIsAtLeast32CharactersLong!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpirationMinutes"] = "30"
            })
            .Build();

        _tokenService = new JwtTokenService(config);
    }

    [Fact]
    public void GenerateAccessToken_ReturnsNonEmptyToken()
    {
        var user = CreateTestUser();

        var token = _tokenService.GenerateAccessToken(user);

        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3); // JWT has 3 parts
    }

    [Fact]
    public void GenerateAccessToken_ContainsCorrectClaims()
    {
        var user = CreateTestUser();

        var token = _tokenService.GenerateAccessToken(user);
        var principal = _tokenService.ValidateToken(token);

        principal.Should().NotBeNull();

        // JWT handler maps "sub" → ClaimTypes.NameIdentifier
        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(user.Id.ToString());
        // JWT handler maps "email" → ClaimTypes.Email
        principal.FindFirst(ClaimTypes.Email)!.Value.Should().Be(user.Email);
        // Custom claim "tenant_id" stays as-is (not mapped by JWT handler)
        principal.FindFirst("tenant_id")!.Value.Should().Be(user.TenantId.ToString());
        // Custom claim "display_name" stays as-is
        principal.FindFirst("display_name")!.Value.Should().Be(user.DisplayName);
        // JWT handler maps "role" → ClaimTypes.Role
        principal.FindFirst(ClaimTypes.Role)!.Value.Should().Be(UserRole.Admin.ToString());
    }

    [Fact]
    public void ValidateToken_ValidToken_ReturnsPrincipal()
    {
        var user = CreateTestUser();
        var token = _tokenService.GenerateAccessToken(user);

        var result = _tokenService.ValidateToken(token);

        result.Should().NotBeNull();
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsNull()
    {
        var result = _tokenService.ValidateToken("invalid.token.here");

        result.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_TamperedToken_ReturnsNull()
    {
        var user = CreateTestUser();
        var token = _tokenService.GenerateAccessToken(user);
        var tampered = token.Substring(0, token.Length - 5) + "XXXXX";

        var result = _tokenService.ValidateToken(tampered);

        result.Should().BeNull();
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyBase64()
    {
        var refreshToken = _tokenService.GenerateRefreshToken();

        refreshToken.Should().NotBeNullOrEmpty();
        // Should be valid base64
        var bytes = Convert.FromBase64String(refreshToken);
        bytes.Should().HaveCount(64); // 64 random bytes
    }

    [Fact]
    public void GenerateRefreshToken_UniqueOnEachCall()
    {
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GetExpirationMinutes_ReturnsConfiguredValue()
    {
        var result = _tokenService.GetExpirationMinutes();

        result.Should().Be(30);
    }

    private static User CreateTestUser() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Email = "test@example.com",
        DisplayName = "Test User",
        PasswordHash = "hashed",
        Role = UserRole.Admin,
        IsActive = true
    };
}
