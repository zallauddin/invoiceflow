using System.Security.Claims;
using FluentAssertions;
using InvoiceFlow.Api.Infrastructure;
using InvoiceFlow.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvoiceFlow.UnitTests.Api;

public class HttpContextTenantIdProviderTests
{
    [Fact]
    public void TenantId_WhenNoHttpContext_ReturnsNull()
    {
        // Arrange - HttpContextAccessor with no HttpContext
        var accessor = new HttpContextAccessor { HttpContext = null };
        var provider = new HttpContextTenantIdProvider(accessor);

        // Act
        var result = provider.TenantId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TenantId_WhenClaimsContainTenantId_ReturnsClaimValue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenant_id", tenantId.ToString())
            }))
        };

        var accessor = new HttpContextAccessor { HttpContext = context };
        var provider = new HttpContextTenantIdProvider(accessor);

        // Act
        var result = provider.TenantId;

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_WhenHeaderContainsTenantId_ReturnsHeaderValue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            Request =
            {
                Headers = { ["X-Tenant-Id"] = tenantId.ToString() }
            }
        };

        var accessor = new HttpContextAccessor { HttpContext = context };
        var provider = new HttpContextTenantIdProvider(accessor);

        // Act
        var result = provider.TenantId;

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_WhenClaimsAndHeaderBothPresent_PrefersClaims()
    {
        // Arrange
        var claimTenantId = Guid.NewGuid();
        var headerTenantId = Guid.NewGuid();

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenant_id", claimTenantId.ToString())
            })),
            Request =
            {
                Headers = { ["X-Tenant-Id"] = headerTenantId.ToString() }
            }
        };

        var accessor = new HttpContextAccessor { HttpContext = context };
        var provider = new HttpContextTenantIdProvider(accessor);

        // Act
        var result = provider.TenantId;

        // Assert
        result.Should().Be(claimTenantId); // Claims take priority
    }

    [Fact]
    public void TenantId_WhenClaimIsInvalid_ReturnsNull()
    {
        // Arrange
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenant_id", "not-a-guid")
            }))
        };

        var accessor = new HttpContextAccessor { HttpContext = context };
        var provider = new HttpContextTenantIdProvider(accessor);

        // Act
        var result = provider.TenantId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TenantId_WhenHeaderIsInvalid_ReturnsNull()
    {
        // Arrange
        var context = new DefaultHttpContext
        {
            Request =
            {
                Headers = { ["X-Tenant-Id"] = "invalid-guid" }
            }
        };

        var accessor = new HttpContextAccessor { HttpContext = context };
        var provider = new HttpContextTenantIdProvider(accessor);

        // Act
        var result = provider.TenantId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ITenantIdProvider_IsRegisteredInterface()
    {
        // Verify the interface contract
        typeof(ITenantIdProvider).Should().HaveProperty(typeof(Guid?), nameof(ITenantIdProvider.TenantId));
    }
}
