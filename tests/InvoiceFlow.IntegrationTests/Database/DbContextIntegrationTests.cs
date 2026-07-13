using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Infrastructure.Auth;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Infrastructure.Data;
using InvoiceFlow.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.IntegrationTests.Database;

/// <summary>
/// Integration tests for InvoiceFlowDbContext against real PostgreSQL via TestContainers.
/// </summary>
public class DbContextIntegrationTests : DatabaseIntegrationTestBase
{
    public DbContextIntegrationTests(TestInfrastructureFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task CanConnectToDatabase()
    {
        using var db = CreateDbContext();
        var canConnect = await db.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }

    [Fact]
    public async Task Tenants_ShouldStartEmpty()
    {
        using var db = CreateDbContext();
        var count = await db.Tenants.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task CanCreateTenant()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db, "Acme Corp", "acme");

        tenant.Id.Should().NotBe(Guid.Empty);
        tenant.Name.Should().Be("Acme Corp");
        tenant.Slug.Should().Be("acme");

        var fetched = await db.Tenants.FindAsync(tenant.Id);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task CanCreateUser_WithTenantRelation()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db);
        var user = await SeedUserAsync(db, tenant.Id, "admin@acme.com", UserRole.Admin);

        var fetched = await db.Users
            .Include(u => u.Tenant)
            .FirstAsync(u => u.Id == user.Id);

        fetched.Tenant.Should().NotBeNull();
        fetched.Tenant!.Name.Should().Be("Test Tenant");
        fetched.Role.Should().Be(UserRole.Admin);
        fetched.Email.Should().Be("admin@acme.com");
    }

    [Fact]
    public async Task CanCreateInvoice_WithTenantRelation()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db);
        var invoice = await SeedInvoiceAsync(db, tenant.Id, "INV-2024-001");

        var fetched = await db.Invoices
            .Include(i => i.Tenant)
            .FirstAsync(i => i.Id == invoice.Id);

        fetched.InvoiceNumber.Should().Be("INV-2024-001");
        fetched.TotalAmount.Should().Be(1200m);
        fetched.Tenant.Should().NotBeNull();
        fetched.Tenant!.Slug.Should().Be("test-tenant");
    }

    [Fact]
    public async Task CanCreateInvoiceLines()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db);
        var invoice = await SeedInvoiceAsync(db, tenant.Id);

        var line = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            LineNumber = 1,
            Description = "Widget",
            Quantity = 10,
            UnitPrice = 50m,
            LineTotal = 500m,
            TaxRate = 20,
            TaxAmount = 100m
        };

        db.InvoiceLines.Add(line);
        await db.SaveChangesAsync();

        var fetched = await db.InvoiceLines
            .Include(l => l.Invoice)
            .FirstAsync(l => l.Id == line.Id);

        fetched.Invoice.Should().NotBeNull();
        fetched.Invoice!.InvoiceNumber.Should().Be("INV-001");
        fetched.Description.Should().Be("Widget");
    }

    [Fact]
    public async Task CanCreateDocument()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db);
        var invoice = await SeedInvoiceAsync(db, tenant.Id);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            FileName = "invoice.pdf",
            MimeType = "application/pdf",
            FileSize = 1024,
            StoragePath = $"{tenant.Id}/invoices/2024/01/invoice.pdf",
            DocumentType = DocumentType.Invoice,
            LinkedInvoiceId = invoice.Id,
            CreatedAt = DateTime.UtcNow
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var fetched = await db.Documents.FindAsync(document.Id);
        fetched.Should().NotBeNull();
        fetched!.FileName.Should().Be("invoice.pdf");
        fetched.LinkedInvoiceId.Should().Be(invoice.Id);
    }

    [Fact]
    public async Task CanCreateRefreshToken()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db);
        var user = await SeedUserAsync(db, tenant.Id);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)),
            UserId = user.Id,
            TenantId = tenant.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        var fetched = await db.RefreshTokens.FindAsync(refreshToken.Id);
        fetched.Should().NotBeNull();
        fetched!.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task CanCreateComplianceConfig()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db);

        var config = new ComplianceConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            CountryCode = "DE",
            Model = ComplianceModel.Peppol,
            IsEnabled = true,
            SandboxMode = true,
            CreatedAt = DateTime.UtcNow
        };

        db.ComplianceConfigs.Add(config);
        await db.SaveChangesAsync();

        var fetched = await db.ComplianceConfigs.FindAsync(config.Id);
        fetched.Should().NotBeNull();
        fetched!.CountryCode.Should().Be("DE");
        fetched.Model.Should().Be(ComplianceModel.Peppol);
    }

    [Fact]
    public async Task CanCreateAuditLog()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db);
        var invoice = await SeedInvoiceAsync(db, tenant.Id);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            InvoiceId = invoice.Id,
            Action = "Created",
            PerformedBy = "system",
            Details = "Invoice created via API",
            CreatedAt = DateTime.UtcNow
        };

        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync();

        var fetched = await db.AuditLogs.FindAsync(auditLog.Id);
        fetched.Should().NotBeNull();
        fetched!.Action.Should().Be("Created");
        fetched.InvoiceId.Should().Be(invoice.Id);
    }

    [Fact]
    public async Task CanCreateApprovalRequest()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db);
        var invoice = await SeedInvoiceAsync(db, tenant.Id, "INV-002", InvoiceStatus.PendingApproval);
        var user = await SeedUserAsync(db, tenant.Id, "approver@acme.com", UserRole.Admin);

        var request = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            TenantId = tenant.Id,
            Status = ApprovalStatus.Pending,
            AssignedToUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        db.ApprovalRequests.Add(request);
        await db.SaveChangesAsync();

        var fetched = await db.ApprovalRequests
            .Include(ar => ar.Invoice)
            .Include(ar => ar.AssignedToUser)
            .FirstAsync(ar => ar.Id == request.Id);

        fetched.Status.Should().Be(ApprovalStatus.Pending);
        fetched.Invoice.InvoiceNumber.Should().Be("INV-002");
        fetched.AssignedToUser!.Email.Should().Be("approver@acme.com");
    }

    [Fact]
    public async Task DomainEvents_ShouldNotBePersisted()
    {
        using var db = CreateDbContext();
        var tenant = await SeedTenantAsync(db);
        var invoice = await SeedInvoiceAsync(db, tenant.Id);

        // Add a domain event to the in-memory entity
        invoice.DomainEvents.Add(new Core.Events.InvoiceReceivedEvent
        {
            InvoiceId = invoice.Id,
            TenantId = tenant.Id,
            Source = "Manual",
            FileName = "test.pdf"
        });

        // SaveChanges won't persist DomainEvents (they're Ignored in DbContext)
        await db.SaveChangesAsync();

        // DomainEvents is not a DbSet — verify entity saved fine
        var fetched = await db.Invoices.FindAsync(invoice.Id);
        fetched.Should().NotBeNull();
    }
}
