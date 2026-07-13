using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.UnitTests.Data;

public class DbContextTests : IDisposable
{
    private readonly InvoiceFlowDbContext _context;

    public DbContextTests()
    {
        var options = new DbContextOptionsBuilder<InvoiceFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new InvoiceFlowDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    #region DbSet Existence

    [Fact]
    public void DbSets_ShouldBeAccessible()
    {
        _context.Tenants.Should().NotBeNull();
        _context.Users.Should().NotBeNull();
        _context.Invoices.Should().NotBeNull();
        _context.InvoiceLines.Should().NotBeNull();
        _context.Documents.Should().NotBeNull();
        _context.ComplianceConfigs.Should().NotBeNull();
        _context.ConnectorConfigs.Should().NotBeNull();
        _context.WebhookConfigs.Should().NotBeNull();
        _context.ApprovalRequests.Should().NotBeNull();
        _context.AuditLogs.Should().NotBeNull();
    }

    [Fact]
    public async Task DbSets_ShouldStartEmpty()
    {
        (await _context.Tenants.CountAsync()).Should().Be(0);
        (await _context.Users.CountAsync()).Should().Be(0);
        (await _context.Invoices.CountAsync()).Should().Be(0);
        (await _context.InvoiceLines.CountAsync()).Should().Be(0);
        (await _context.Documents.CountAsync()).Should().Be(0);
        (await _context.ComplianceConfigs.CountAsync()).Should().Be(0);
        (await _context.ConnectorConfigs.CountAsync()).Should().Be(0);
        (await _context.WebhookConfigs.CountAsync()).Should().Be(0);
        (await _context.ApprovalRequests.CountAsync()).Should().Be(0);
        (await _context.AuditLogs.CountAsync()).Should().Be(0);
    }

    #endregion

    #region Tenant Isolation — Global Query Filters

    [Fact]
    public async Task SetTenantId_ShouldFilterInvoices()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _context.Invoices.Add(new Invoice { Id = Guid.NewGuid(), TenantId = tenantA, InvoiceNumber = "INV-001", VendorName = "A", BuyerName = "B", InvoiceDate = DateTime.UtcNow });
        _context.Invoices.Add(new Invoice { Id = Guid.NewGuid(), TenantId = tenantB, InvoiceNumber = "INV-002", VendorName = "C", BuyerName = "D", InvoiceDate = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        // No filter set — should see all
        _context.SetTenantId(null);
        (await _context.Invoices.CountAsync()).Should().Be(2);

        // Filter to tenant A — should see 1
        _context.SetTenantId(tenantA);
        (await _context.Invoices.CountAsync()).Should().Be(1);
        (await _context.Invoices.FirstAsync()).InvoiceNumber.Should().Be("INV-001");
    }

    [Fact]
    public async Task SetTenantId_ShouldFilterDocuments()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _context.Documents.Add(new Document { Id = Guid.NewGuid(), TenantId = tenantA, FileName = "a.pdf", MimeType = "application/pdf", FileSize = 100, StoragePath = "/a", DocumentType = DocumentType.Invoice });
        _context.Documents.Add(new Document { Id = Guid.NewGuid(), TenantId = tenantB, FileName = "b.pdf", MimeType = "application/pdf", FileSize = 200, StoragePath = "/b", DocumentType = DocumentType.CreditNote });
        await _context.SaveChangesAsync();

        _context.SetTenantId(tenantA);
        (await _context.Documents.CountAsync()).Should().Be(1);
        (await _context.Documents.FirstAsync()).FileName.Should().Be("a.pdf");
    }

    [Fact]
    public async Task SetTenantId_ShouldFilterUsers()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _context.Users.Add(new User { Id = Guid.NewGuid(), TenantId = tenantA, Email = "a@test.com", DisplayName = "A", PasswordHash = "hash" });
        _context.Users.Add(new User { Id = Guid.NewGuid(), TenantId = tenantB, Email = "b@test.com", DisplayName = "B", PasswordHash = "hash" });
        await _context.SaveChangesAsync();

        _context.SetTenantId(tenantB);
        (await _context.Users.CountAsync()).Should().Be(1);
        (await _context.Users.FirstAsync()).Email.Should().Be("b@test.com");
    }

    [Fact]
    public async Task SetTenantId_ShouldFilterAuditLogs()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _context.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = tenantA, Action = "created" });
        _context.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = tenantB, Action = "updated" });
        await _context.SaveChangesAsync();

        _context.SetTenantId(tenantA);
        (await _context.AuditLogs.CountAsync()).Should().Be(1);
        (await _context.AuditLogs.FirstAsync()).Action.Should().Be("created");
    }

    [Fact]
    public async Task SetTenantId_ShouldNotFilterTenants()
    {
        _context.Tenants.Add(new Tenant { Id = Guid.NewGuid(), Name = "T1", Slug = "t1" });
        _context.Tenants.Add(new Tenant { Id = Guid.NewGuid(), Name = "T2", Slug = "t2" });
        await _context.SaveChangesAsync();

        _context.SetTenantId(Guid.NewGuid());
        (await _context.Tenants.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SetTenantId_ShouldNotDirectlyFilterInvoiceLines()
    {
        var tenantA = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        _context.Invoices.Add(new Invoice { Id = invoiceId, TenantId = tenantA, InvoiceNumber = "INV-001", VendorName = "V", BuyerName = "B", InvoiceDate = DateTime.UtcNow });
        _context.InvoiceLines.Add(new InvoiceLine { Id = Guid.NewGuid(), InvoiceId = invoiceId, LineNumber = 1, Description = "Line 1", Quantity = 1, UnitPrice = 10, LineTotal = 10, TaxRate = 0, TaxAmount = 0 });
        await _context.SaveChangesAsync();

        // Setting a different tenant — InvoiceLines don't have a direct filter
        _context.SetTenantId(Guid.NewGuid());
        // InvoiceLines should still be visible (no direct query filter on InvoiceLine)
        (await _context.InvoiceLines.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SetTenantId_ShouldFilterApprovalRequests()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        _context.Invoices.Add(new Invoice { Id = invoiceId, TenantId = tenantA, InvoiceNumber = "INV-001", VendorName = "V", BuyerName = "B", InvoiceDate = DateTime.UtcNow });
        _context.ApprovalRequests.Add(new ApprovalRequest { Id = Guid.NewGuid(), InvoiceId = invoiceId, TenantId = tenantA, Status = ApprovalStatus.Pending });
        _context.ApprovalRequests.Add(new ApprovalRequest { Id = Guid.NewGuid(), InvoiceId = invoiceId, TenantId = tenantB, Status = ApprovalStatus.Approved });
        await _context.SaveChangesAsync();

        _context.SetTenantId(tenantA);
        (await _context.ApprovalRequests.CountAsync()).Should().Be(1);
        (await _context.ApprovalRequests.FirstAsync()).Status.Should().Be(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task SetTenantId_ShouldFilterComplianceConfigs()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _context.ComplianceConfigs.Add(new ComplianceConfig { Id = Guid.NewGuid(), TenantId = tenantA, CountryCode = "DE", Model = ComplianceModel.Peppol });
        _context.ComplianceConfigs.Add(new ComplianceConfig { Id = Guid.NewGuid(), TenantId = tenantB, CountryCode = "FR", Model = ComplianceModel.Zatca });
        await _context.SaveChangesAsync();

        _context.SetTenantId(tenantB);
        (await _context.ComplianceConfigs.CountAsync()).Should().Be(1);
        (await _context.ComplianceConfigs.FirstAsync()).CountryCode.Should().Be("FR");
    }

    [Fact]
    public async Task SetTenantId_ShouldFilterConnectorConfigs()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _context.ConnectorConfigs.Add(new ConnectorConfig { Id = Guid.NewGuid(), TenantId = tenantA, ConnectorType = ConnectorType.Sap });
        _context.ConnectorConfigs.Add(new ConnectorConfig { Id = Guid.NewGuid(), TenantId = tenantB, ConnectorType = ConnectorType.Oracle });
        await _context.SaveChangesAsync();

        _context.SetTenantId(tenantA);
        (await _context.ConnectorConfigs.CountAsync()).Should().Be(1);
        (await _context.ConnectorConfigs.FirstAsync()).ConnectorType.Should().Be(ConnectorType.Sap);
    }

    [Fact]
    public async Task SetTenantId_ShouldFilterWebhookConfigs()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _context.WebhookConfigs.Add(new WebhookConfig { Id = Guid.NewGuid(), TenantId = tenantA, Name = "WH-A", Url = "https://a.com", Secret = "s1", Events = new List<WebhookEventType> { WebhookEventType.InvoiceReceived } });
        _context.WebhookConfigs.Add(new WebhookConfig { Id = Guid.NewGuid(), TenantId = tenantB, Name = "WH-B", Url = "https://b.com", Secret = "s2", Events = new List<WebhookEventType> { WebhookEventType.InvoiceApproved } });
        await _context.SaveChangesAsync();

        _context.SetTenantId(tenantB);
        (await _context.WebhookConfigs.CountAsync()).Should().Be(1);
        (await _context.WebhookConfigs.FirstAsync()).Name.Should().Be("WH-B");
    }

    #endregion

    #region DomainEvents Exclusion

    [Fact]
    public async Task DomainEvents_ShouldNotBePersisted()
    {
        var tenantId = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "INV-001",
            VendorName = "Test Vendor",
            BuyerName = "Test Buyer",
            InvoiceDate = DateTime.UtcNow
        };
        invoice.DomainEvents.Add(new InvoiceReceivedEvent
        {
            InvoiceId = invoice.Id,
            TenantId = tenantId,
            Source = "email",
            FileName = "test.pdf"
        });

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        // Reload — DomainEvents should be empty (not persisted via Ignore)
        _context.ChangeTracker.Clear();
        var loaded = await _context.Invoices.FindAsync(invoice.Id);
        loaded.Should().NotBeNull();
        loaded!.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region WebhookConfig.Events JSON Conversion

    [Fact]
    public async Task WebhookConfig_Events_ShouldRoundTripAsJson()
    {
        var tenantId = Guid.NewGuid();
        var events = new List<WebhookEventType>
        {
            WebhookEventType.InvoiceReceived,
            WebhookEventType.InvoiceApproved,
            WebhookEventType.InvoiceRejected
        };

        _context.WebhookConfigs.Add(new WebhookConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Test Webhook",
            Url = "https://test.com/webhook",
            Secret = "secret123",
            Events = events
        });
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var loaded = await _context.WebhookConfigs.FirstAsync(w => w.TenantId == tenantId);
        loaded.Events.Should().HaveCount(3);
        loaded.Events.Should().Contain(WebhookEventType.InvoiceReceived);
        loaded.Events.Should().Contain(WebhookEventType.InvoiceApproved);
        loaded.Events.Should().Contain(WebhookEventType.InvoiceRejected);
    }

    #endregion

    #region CRUD Operations

    [Fact]
    public async Task AddAndRetrieveEntity_ShouldWork()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Test Tenant", Slug = "test-tenant" };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        var loaded = await _context.Tenants.FindAsync(tenant.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Test Tenant");
        loaded.Slug.Should().Be("test-tenant");
    }

    [Fact]
    public async Task UpdateEntity_ShouldPersistChanges()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Old Name", Slug = "old" };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        tenant.Name = "New Name";
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var loaded = await _context.Tenants.FindAsync(tenant.Id);
        loaded!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task DeleteEntity_ShouldRemove()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "To Delete", Slug = "delete" };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync();

        (await _context.Tenants.CountAsync()).Should().Be(0);
    }

    #endregion
}
