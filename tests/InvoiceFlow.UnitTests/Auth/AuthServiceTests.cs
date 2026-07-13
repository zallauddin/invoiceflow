using FluentAssertions;
using InvoiceFlow.Core.DTOs.Auth;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Auth;
using InvoiceFlow.Infrastructure.Data;
using InvoiceFlow.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace InvoiceFlow.UnitTests.Auth;

public class AuthServiceTests : IDisposable
{
    private readonly InvoiceFlowDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ITenantResolver _tenantResolver;
    private readonly IUserRepository _userRepository;
    private readonly AuthService _authService;

    private readonly Tenant _testTenant;
    private readonly User _testUser;

    public AuthServiceTests()
    {
        // In-memory DB for testing
        var options = new DbContextOptionsBuilder<InvoiceFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new InvoiceFlowDbContext(options);

        // Mock token service
        _tokenService = Substitute.For<ITokenService>();
        _tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("mock-access-token");
        _tokenService.GenerateRefreshToken().Returns("mock-refresh-token");
        _tokenService.GetExpirationMinutes().Returns(60);

        // Real tenant resolver (uses in-memory DB)
        _tenantResolver = new DatabaseTenantResolver(_context);

        // Real user repository (uses in-memory DB)
        _userRepository = new UserRepository(_context);

        // System under test
        _authService = new AuthService(_context, _userRepository, _tenantResolver, _tokenService);

        // Seed test data
        _testTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Corp",
            Slug = "test-corp",
            Country = "DE",
            IsActive = true
        };

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenant.Id,
            Email = "admin@test-corp.com",
            DisplayName = "Test Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        _context.Tenants.Add(_testTenant);
        _context.Users.Add(_testUser);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // --- Login Tests ---

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponse()
    {
        var request = new LoginRequest
        {
            Email = "admin@test-corp.com",
            Password = "Test1234!",
            TenantSlug = "test-corp"
        };

        var result = await _authService.LoginAsync(request);

        result.Should().NotBeNull();
        result.AccessToken.Should().Be("mock-access-token");
        result.RefreshToken.Should().Be("mock-refresh-token");
        result.User.Email.Should().Be("admin@test-corp.com");
        result.User.Role.Should().Be(UserRole.Admin);
        result.User.TenantId.Should().Be(_testTenant.Id);
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ThrowsInvalidOperationException()
    {
        var request = new LoginRequest
        {
            Email = "admin@test-corp.com",
            Password = "WrongPassword!",
            TenantSlug = "test-corp"
        };

        var act = () => _authService.LoginAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public async Task LoginAsync_NonExistentEmail_ThrowsInvalidOperationException()
    {
        var request = new LoginRequest
        {
            Email = "nobody@test-corp.com",
            Password = "Test1234!",
            TenantSlug = "test-corp"
        };

        var act = () => _authService.LoginAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public async Task LoginAsync_InactiveTenant_ThrowsInvalidOperationException()
    {
        _testTenant.IsActive = false;
        await _context.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "admin@test-corp.com",
            Password = "Test1234!",
            TenantSlug = "test-corp"
        };

        var act = () => _authService.LoginAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ThrowsInvalidOperationException()
    {
        _testUser.IsActive = false;
        await _context.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "admin@test-corp.com",
            Password = "Test1234!",
            TenantSlug = "test-corp"
        };

        var act = () => _authService.LoginAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task LoginAsync_NonExistentTenant_ThrowsInvalidOperationException()
    {
        var request = new LoginRequest
        {
            Email = "admin@test-corp.com",
            Password = "Test1234!",
            TenantSlug = "nonexistent"
        };

        var act = () => _authService.LoginAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public async Task LoginAsync_ValidLogin_UpdatesLastLoginAt()
    {
        var request = new LoginRequest
        {
            Email = "admin@test-corp.com",
            Password = "Test1234!",
            TenantSlug = "test-corp"
        };

        await _authService.LoginAsync(request);

        var user = await _context.Users.FindAsync(_testUser.Id);
        user!.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // --- Register Tests ---

    [Fact]
    public async Task RegisterAsync_ValidRequest_ReturnsAuthResponse()
    {
        var request = new RegisterRequest
        {
            Email = "newuser@test-corp.com",
            Password = "Secure123!",
            DisplayName = "New User",
            TenantId = _testTenant.Id,
            Role = UserRole.User
        };

        var result = await _authService.RegisterAsync(request);

        result.Should().NotBeNull();
        result.User.Email.Should().Be("newuser@test-corp.com");
        result.User.DisplayName.Should().Be("New User");
        result.User.Role.Should().Be(UserRole.User);
        result.User.TenantId.Should().Be(_testTenant.Id);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var request = new RegisterRequest
        {
            Email = "admin@test-corp.com", // Already exists
            Password = "Secure123!",
            DisplayName = "Dupe User",
            TenantId = _testTenant.Id
        };

        var act = () => _authService.RegisterAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task RegisterAsync_NonExistentTenant_ThrowsInvalidOperationException()
    {
        var request = new RegisterRequest
        {
            Email = "newuser@test.com",
            Password = "Secure123!",
            DisplayName = "New User",
            TenantId = Guid.NewGuid()
        };

        var act = () => _authService.RegisterAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid tenant*");
    }

    // --- Refresh Token Tests ---

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        // First, login to get a refresh token
        var loginRequest = new LoginRequest
        {
            Email = "admin@test-corp.com",
            Password = "Test1234!",
            TenantSlug = "test-corp"
        };
        var loginResult = await _authService.LoginAsync(loginRequest);

        // Now refresh
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginResult.RefreshToken
        };

        // Mock for new tokens
        _tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("new-access-token");
        _tokenService.GenerateRefreshToken().Returns("new-refresh-token");

        var result = await _authService.RefreshTokenAsync(refreshRequest);

        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_ThrowsInvalidOperationException()
    {
        var request = new RefreshTokenRequest
        {
            RefreshToken = "nonexistent-token"
        };

        var act = () => _authService.RefreshTokenAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid refresh token*");
    }

    [Fact]
    public async Task RefreshTokenAsync_RevokedToken_ThrowsInvalidOperationException()
    {
        // First, login to get a refresh token
        var loginRequest = new LoginRequest
        {
            Email = "admin@test-corp.com",
            Password = "Test1234!",
            TenantSlug = "test-corp"
        };
        var loginResult = await _authService.LoginAsync(loginRequest);

        // Revoke it
        await _authService.RevokeRefreshTokenAsync(loginResult.RefreshToken);

        // Try to refresh with revoked token
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginResult.RefreshToken
        };

        var act = () => _authService.RefreshTokenAsync(refreshRequest);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired or revoked*");
    }

    // --- Logout Tests ---

    [Fact]
    public async Task RevokeRefreshTokenAsync_ValidToken_RevokesSuccessfully()
    {
        // First, login to get a refresh token
        var loginRequest = new LoginRequest
        {
            Email = "admin@test-corp.com",
            Password = "Test1234!",
            TenantSlug = "test-corp"
        };
        var loginResult = await _authService.LoginAsync(loginRequest);

        // Revoke
        await _authService.RevokeRefreshTokenAsync(loginResult.RefreshToken);

        // Verify it's revoked in DB
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == loginResult.RefreshToken);
        storedToken.Should().NotBeNull();
        storedToken!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_NonExistentToken_DoesNotThrow()
    {
        // Should not throw — idempotent
        var act = () => _authService.RevokeRefreshTokenAsync("nonexistent");
        await act.Should().NotThrowAsync();
    }
}
