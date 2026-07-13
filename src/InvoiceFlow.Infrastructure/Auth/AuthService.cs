using InvoiceFlow.Core.DTOs.Auth;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Auth;

/// <summary>
/// Auth service implementing JWT access/refresh token flow.
/// Uses BCrypt for password hashing and a custom token service for JWT generation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly InvoiceFlowDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly ITenantResolver _tenantResolver;
    private readonly ITokenService _tokenService;

    private const int RefreshTokenExpirationDays = 30;

    public AuthService(
        InvoiceFlowDbContext context,
        IUserRepository userRepository,
        ITenantResolver tenantResolver,
        ITokenService tokenService)
    {
        _context = context;
        _userRepository = userRepository;
        _tenantResolver = tenantResolver;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Resolve tenant by slug
        var tenant = await _tenantResolver.GetBySlugAsync(request.TenantSlug, cancellationToken)
            ?? throw new InvalidOperationException("Invalid tenant or credentials.");

        if (!tenant.IsActive)
            throw new InvalidOperationException("Tenant is inactive.");

        // Find user by email within tenant
        var user = await _userRepository.GetByEmailAsync(tenant.Id, request.Email, cancellationToken)
            ?? throw new InvalidOperationException("Invalid tenant or credentials.");

        if (!user.IsActive)
            throw new InvalidOperationException("User account is inactive.");

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new InvalidOperationException("Invalid tenant or credentials.");

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // Verify tenant exists and is active
        var tenant = await _tenantResolver.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Invalid tenant.");

        if (!tenant.IsActive)
            throw new InvalidOperationException("Tenant is inactive.");

        // Check for duplicate email within tenant
        if (await _userRepository.ExistsAsync(request.TenantId, request.Email, cancellationToken))
            throw new InvalidOperationException("A user with this email already exists in this tenant.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Email = request.Email.ToLowerInvariant(),
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var storedToken = await _context.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken)
            ?? throw new InvalidOperationException("Invalid refresh token.");

        if (!storedToken.IsActive)
            throw new InvalidOperationException("Refresh token is expired or revoked.");

        // Get the user
        var user = await _userRepository.GetByIdAsync(storedToken.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User no longer exists.");

        if (!user.IsActive)
            throw new InvalidOperationException("User account is inactive.");

        // Revoke the old refresh token (rotate)
        storedToken.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var storedToken = await _context.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, cancellationToken);

        if (storedToken is not null && !storedToken.IsRevoked)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(_tokenService.GetExpirationMinutes());

        // Store refresh token
        var storedRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshTokenValue,
            UserId = user.Id,
            TenantId = user.TenantId,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<RefreshToken>().Add(storedRefreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                TenantId = user.TenantId,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = user.Role,
                IsActive = user.IsActive
            }
        };
    }
}
