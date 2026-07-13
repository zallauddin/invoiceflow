using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Infrastructure.Data;
using InvoiceFlow.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.UnitTests.Data;

public class RepositoryTests : IDisposable
{
    private readonly InvoiceFlowDbContext _context;
    private readonly Repository<Tenant> _repository;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<InvoiceFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new InvoiceFlowDbContext(options);
        _repository = new Repository<Tenant>(_context);
    }

    public void Dispose() => _context.Dispose();

    #region AddAsync

    [Fact]
    public async Task AddAsync_ShouldPersistEntity()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Test", Slug = "test" };

        var result = await _repository.AddAsync(tenant);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();

        var loaded = await _context.Tenants.FindAsync(tenant.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Test");
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ExistingEntity_ShouldReturn()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Find Me", Slug = "find" };
        await _repository.AddAsync(tenant);

        var result = await _repository.GetByIdAsync(tenant.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Find Me");
    }

    [Fact]
    public async Task GetByIdAsync_NonExisting_ShouldReturnNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_ShouldReturnPaginated()
    {
        for (int i = 0; i < 10; i++)
        {
            await _repository.AddAsync(new Tenant { Id = Guid.NewGuid(), Name = $"T{i}", Slug = $"t{i}" });
        }

        var page1 = await _repository.GetAllAsync(skip: 0, take: 5);
        page1.Should().HaveCount(5);

        var page2 = await _repository.GetAllAsync(skip: 5, take: 5);
        page2.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAllAsync_EmptyDb_ShouldReturnEmpty()
    {
        var result = await _repository.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_DefaultPagination_ShouldReturnUpTo100()
    {
        for (int i = 0; i < 5; i++)
        {
            await _repository.AddAsync(new Tenant { Id = Guid.NewGuid(), Name = $"T{i}", Slug = $"t{i}" });
        }

        var result = await _repository.GetAllAsync();
        result.Should().HaveCount(5);
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Before", Slug = "before" };
        await _repository.AddAsync(tenant);

        tenant.Name = "After";
        await _repository.UpdateAsync(tenant);

        _context.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(tenant.Id);
        loaded!.Name.Should().Be("After");
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ShouldRemove()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Delete", Slug = "delete" };
        await _repository.AddAsync(tenant);

        await _repository.DeleteAsync(tenant);

        var loaded = await _repository.GetByIdAsync(tenant.Id);
        loaded.Should().BeNull();
    }

    #endregion

    #region ExistsAsync

    [Fact]
    public async Task ExistsAsync_Existing_ShouldReturnTrue()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Exists", Slug = "exists" };
        await _repository.AddAsync(tenant);

        (await _repository.ExistsAsync(tenant.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExisting_ShouldReturnFalse()
    {
        (await _repository.ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }

    #endregion

    #region CountAsync

    [Fact]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        (await _repository.CountAsync()).Should().Be(0);

        await _repository.AddAsync(new Tenant { Id = Guid.NewGuid(), Name = "A", Slug = "a" });
        (await _repository.CountAsync()).Should().Be(1);

        await _repository.AddAsync(new Tenant { Id = Guid.NewGuid(), Name = "B", Slug = "b" });
        (await _repository.CountAsync()).Should().Be(2);
    }

    #endregion

    #region Tenant Filtering Integration

    [Fact]
    public async Task Repository_WithTenantFilter_ShouldRespectGlobalQueryFilter()
    {
        // Use a Repository<Invoice> to test tenant filtering through the repository
        var invoiceRepo = new Repository<Invoice>(_context);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await invoiceRepo.AddAsync(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            InvoiceNumber = "INV-A1",
            VendorName = "VA",
            BuyerName = "BA",
            InvoiceDate = DateTime.UtcNow
        });
        await invoiceRepo.AddAsync(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            InvoiceNumber = "INV-B1",
            VendorName = "VB",
            BuyerName = "BB",
            InvoiceDate = DateTime.UtcNow
        });

        // Set filter to tenant A
        _context.SetTenantId(tenantA);

        var allInvoices = await invoiceRepo.GetAllAsync();
        allInvoices.Should().HaveCount(1);
        allInvoices[0].InvoiceNumber.Should().Be("INV-A1");
        (await invoiceRepo.CountAsync()).Should().Be(1);
    }

    #endregion

    #region Invoice Repository Integration

    [Fact]
    public async Task InvoiceRepository_Crud_ShouldWork()
    {
        var invoiceRepo = new Repository<Invoice>(_context);
        var invoiceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var invoice = new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            InvoiceNumber = "INV-2025-001",
            VendorName = "Acme Corp",
            BuyerName = "Globex Inc",
            InvoiceDate = new DateTime(2025, 6, 1),
            Currency = "EUR",
            Subtotal = 1000m,
            TaxAmount = 190m,
            TotalAmount = 1190m
        };

        // Add
        var added = await invoiceRepo.AddAsync(invoice);
        added.Id.Should().Be(invoiceId);

        // Read
        var loaded = await invoiceRepo.GetByIdAsync(invoiceId);
        loaded.Should().NotBeNull();
        loaded!.InvoiceNumber.Should().Be("INV-2025-001");
        loaded.Currency.Should().Be("EUR");
        loaded.TotalAmount.Should().Be(1190m);

        // Update
        loaded.Notes = "Updated via repository";
        await invoiceRepo.UpdateAsync(loaded);
        _context.ChangeTracker.Clear();
        var updated = await invoiceRepo.GetByIdAsync(invoiceId);
        updated!.Notes.Should().Be("Updated via repository");

        // Exists
        (await invoiceRepo.ExistsAsync(invoiceId)).Should().BeTrue();
        (await invoiceRepo.CountAsync()).Should().Be(1);

        // Delete
        await invoiceRepo.DeleteAsync(updated!);
        (await invoiceRepo.GetByIdAsync(invoiceId)).Should().BeNull();
    }

    #endregion

    #region InvoiceLine Repository (No Direct Tenant Filter)

    [Fact]
    public async Task InvoiceLineRepository_ShouldWorkWithoutTenantFilter()
    {
        var invoiceId = Guid.NewGuid();
        _context.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            TenantId = Guid.NewGuid(),
            InvoiceNumber = "INV-001",
            VendorName = "V",
            BuyerName = "B",
            InvoiceDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var lineRepo = new Repository<InvoiceLine>(_context);
        var line = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            LineNumber = 1,
            Description = "Widget",
            Quantity = 10,
            UnitPrice = 5.50m,
            LineTotal = 55.00m,
            TaxRate = 19,
            TaxAmount = 10.45m
        };

        await lineRepo.AddAsync(line);

        var loaded = await lineRepo.GetByIdAsync(line.Id);
        loaded.Should().NotBeNull();
        loaded!.Description.Should().Be("Widget");
        loaded.LineTotal.Should().Be(55.00m);
    }

    #endregion
}
