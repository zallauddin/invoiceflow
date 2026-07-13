using System.Security.Claims;
using InvoiceFlow.Core.DTOs.Auth;
using InvoiceFlow.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// Authentication endpoints: login, register, refresh, logout, and current user info.
/// Uses minimal API pattern with endpoint groups.
/// </summary>
public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .RequireAuthorization(); // Default: require auth (overrides applied per endpoint)

        // POST /api/auth/login — Public
        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            IAuthService authService) =>
        {
            try
            {
                var response = await authService.LoginAsync(request);
                return Results.Ok(response);
            }
            catch (InvalidOperationException)
            {
                return Results.Unauthorized();
            }
        })
        .WithName("Login")
        .WithSummary("Authenticate user and receive JWT tokens")
        .AllowAnonymous(); // Public endpoint

        // POST /api/auth/register — Public (creates new user in tenant)
        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            IAuthService authService) =>
        {
            try
            {
                var response = await authService.RegisterAsync(request);
                return Results.Created($"/api/auth/me", response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("Register")
        .WithSummary("Register a new user within a tenant")
        .AllowAnonymous(); // Public endpoint

        // POST /api/auth/refresh — Public
        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenRequest request,
            IAuthService authService) =>
        {
            try
            {
                var response = await authService.RefreshTokenAsync(request);
                return Results.Ok(response);
            }
            catch (InvalidOperationException)
            {
                return Results.Unauthorized();
            }
        })
        .WithName("RefreshToken")
        .WithSummary("Refresh access token using refresh token")
        .AllowAnonymous(); // Public endpoint

        // POST /api/auth/logout — Requires auth
        group.MapPost("/logout", async (
            [FromBody] RefreshTokenRequest request,
            IAuthService authService) =>
        {
            await authService.RevokeRefreshTokenAsync(request.RefreshToken);
            return Results.Ok(new { message = "Logged out successfully." });
        })
        .WithName("Logout")
        .WithSummary("Revoke refresh token (logout)");

        // GET /api/auth/me — Requires auth
        group.MapGet("/me", (ClaimsPrincipal user) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst("sub")?.Value;
            var email = user.FindFirst(ClaimTypes.Email)?.Value
                        ?? user.FindFirst("email")?.Value;
            var tenantId = user.FindFirst("tenant_id")?.Value;
            var displayName = user.FindFirst("display_name")?.Value;
            var role = user.FindFirst(ClaimTypes.Role)?.Value;

            return Results.Ok(new
            {
                id = userId,
                email,
                tenantId,
                displayName,
                role
            });
        })
        .WithName("GetCurrentUser")
        .WithSummary("Get current authenticated user info");

        return app;
    }
}
