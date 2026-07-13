using System.Security.Claims;
using InvoiceFlow.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvoiceFlow.Api.Infrastructure;

/// <summary>
/// Resolves the current tenant ID from the HTTP request context.
/// Checks JWT claims first, then the X-Tenant-Id header as fallback.
/// Returns null if no tenant context is available (e.g., during startup or admin operations).
/// </summary>
public class HttpContextTenantIdProvider : ITenantIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
                return null;

            // 1. Try JWT claim first (preferred — set during authentication)
            var tenantClaim = httpContext.User?.FindFirst("tenant_id");
            if (tenantClaim is not null && Guid.TryParse(tenantClaim.Value, out var tenantId))
                return tenantId;

            // 2. Fallback to X-Tenant-Id header (for service-to-service or pre-auth scenarios)
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue)
                && Guid.TryParse(headerValue.ToString(), out var headerTenantId))
                return headerTenantId;

            return null;
        }
    }
}
