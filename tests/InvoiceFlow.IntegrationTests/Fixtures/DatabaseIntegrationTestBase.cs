using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.IntegrationTests.Fixtures;

/// <summary>
/// Base class for integration tests that need database access.
/// Provides helper methods for common test operations.
/// </summary>
[Collection("Postgres")]
public abstract class DatabaseIntegrationTestBase : IClassFixture<TestInfrastructureFixture>, IAsyncLifetime
{
    protected readonly TestInfrastructureFixture Fixture;

    protected DatabaseIntegrationTestBase(TestInfrastructureFixture fixture)
    {
        Fixture = fixture;
    }

    /// <summary>
    /// Creates a fresh InvoiceFlowDbContext from the test DI scope.
    /// </summary>
    protected InvoiceFlowDbContext CreateDbContext()
    {
        using var scope = Fixture.CreateScope();
        return scope.ServiceProvider.GetRequiredService<InvoiceFlowDbContext>();
    }

    /// <summary>
    /// Seeds a test tenant into the database.
    /// </summary>
    protected async Task<Tenant> SeedTenantAsync(InvoiceFlowDbContext db, string name = "Test Tenant", string slug = "test-tenant")
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    /// <summary>
    /// Seeds a test user into the database.
    /// </summary>
    protected async Task<User> SeedUserAsync(InvoiceFlowDbContext db, Guid tenantId, string email = "test@example.com", UserRole role = UserRole.User)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            DisplayName = "Test User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Seeds a test invoice into the database.
    /// </summary>
    protected async Task<Invoice> SeedInvoiceAsync(InvoiceFlowDbContext db, Guid tenantId, string invoiceNumber = "INV-001", InvoiceStatus status = InvoiceStatus.Draft)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            DocumentType = DocumentType.Invoice,
            InvoiceDate = DateTime.UtcNow,
            VendorName = "Test Vendor",
            BuyerName = "Test Buyer",
            Currency = "EUR",
            Subtotal = 1000m,
            TaxAmount = 200m,
            TotalAmount = 1200m,
            Status = status,
            Source = IngestionSource.Manual,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;
    public virtual Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// Base class for tests that need RabbitMQ access.
/// </summary>
[Collection("RabbitMq")]
public abstract class RabbitMqIntegrationTestBase : IClassFixture<RabbitMqFixture>
{
    protected readonly RabbitMqFixture Fixture;

    protected RabbitMqIntegrationTestBase(RabbitMqFixture fixture)
    {
        Fixture = fixture;
    }
}

/// <summary>
/// Base class for tests that need Redis access.
/// </summary>
[Collection("Redis")]
public abstract class RedisIntegrationTestBase : IClassFixture<RedisFixture>
{
    protected readonly RedisFixture Fixture;

    protected RedisIntegrationTestBase(RedisFixture fixture)
    {
        Fixture = fixture;
    }
}
