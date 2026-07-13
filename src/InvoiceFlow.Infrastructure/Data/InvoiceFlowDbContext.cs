using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Data;

/// <summary>
/// Primary EF Core DbContext for InvoiceFlow. Manages all entity mappings,
/// composite indexes, tenant isolation via global query filters, and
/// value conversions (e.g., WebhookConfig.Events stored as JSON).
/// </summary>
public class InvoiceFlowDbContext : DbContext
{
    // Captured by query filter expression trees — read at query execution time.
    // Each DbContext instance has its own value, so per-request isolation works.
    private Guid? _filterTenantId;

    public InvoiceFlowDbContext(DbContextOptions<InvoiceFlowDbContext> options)
        : base(options)
    {
    }

    public InvoiceFlowDbContext(DbContextOptions<InvoiceFlowDbContext> options, ITenantIdProvider tenantIdProvider)
        : base(options)
    {
        _filterTenantId = tenantIdProvider?.TenantId;
    }

    /// <summary>Sets the tenant filter for queries executed through this context instance.</summary>
    public void SetTenantId(Guid? tenantId) => _filterTenantId = tenantId;

    /// <summary>Current tenant ID used by global query filters.</summary>
    public Guid? CurrentTenantId => _filterTenantId;

    // --- Core Entities ---
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Document> Documents => Set<Document>();

    // --- Business Document Entities ---
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<DebitNote> DebitNotes => Set<DebitNote>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    // --- Document Management Entities ---
    public DbSet<DocumentLine> DocumentLines => Set<DocumentLine>();
    public DbSet<DocumentVersionHistory> DocumentVersionHistories => Set<DocumentVersionHistory>();
    public DbSet<DocumentRelationship> DocumentRelationships => Set<DocumentRelationship>();

    // --- Configuration Entities ---
    public DbSet<ComplianceConfig> ComplianceConfigs => Set<ComplianceConfig>();
    public DbSet<ConnectorConfig> ConnectorConfigs => Set<ConnectorConfig>();
    public DbSet<WebhookConfig> WebhookConfigs => Set<WebhookConfig>();

    // --- Workflow Entities ---
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    // --- Audit ---
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // --- Auth ---
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the Data/Configurations folder
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoiceFlowDbContext).Assembly);

        // Explicitly exclude DomainEvents from persistence on all entity types that inherit it.
        // Invoice has its own DomainEvents property (parallel to DocumentEntity).
        // DocumentEntity-derived classes inherit DomainEvents from the abstract base —
        // the Ignore is configured once in DocumentEntityConfiguration (TPC requires it on root).
        modelBuilder.Entity<Invoice>().Ignore(e => e.DomainEvents);

        // Apply tenant isolation global query filters.
        // The lambda captures _filterTenantId — EF Core reads it at query execution time.
        ApplyTenantFilters(modelBuilder);
    }

    /// <summary>
    /// Auto-populates UpdatedAt on entities that have editable ModifiedAt timestamps,
    /// and CreatedAt on newly-added entities if not already set.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <inheritdoc cref="SaveChanges"/>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is Invoice
                     || e.Entity is CreditNote
                     || e.Entity is DebitNote
                     || e.Entity is PurchaseOrder
                     || e.Entity is DeliveryNote
                     || e.Entity is Reminder
                     || e.Entity is Document
                     || e.Entity is DocumentRelationship);
        foreach (var entry in entries)
        {
            // For entities with an UpdatedAt property, set it to UtcNow on Modify.
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.GetType().GetProperty("UpdatedAt")?.SetValue(
                    entry.Entity, DateTime.UtcNow);
            }
            // For newly added entities with CreatedAt, ensure it's set (defensive — entity default may already be UtcNow)
            else if (entry.State == EntityState.Added)
            {
                var createdAtProp = entry.Entity.GetType().GetProperty("CreatedAt");
                if (createdAtProp != null)
                {
                    var current = (DateTime?)createdAtProp.GetValue(entry.Entity);
                    if (current == null || current == default)
                        createdAtProp.SetValue(entry.Entity, DateTime.UtcNow);
                }
            }
        }
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        // When _filterTenantId is null, filter is effectively disabled (returns all rows).
        // When set, only rows matching the tenant are returned.
        modelBuilder.Entity<Invoice>().HasQueryFilter(
            e => _filterTenantId == null || e.TenantId == _filterTenantId);
        modelBuilder.Entity<Document>().HasQueryFilter(
            e => _filterTenantId == null || e.TenantId == _filterTenantId);
        modelBuilder.Entity<User>().HasQueryFilter(
            e => _filterTenantId == null || e.TenantId == _filterTenantId);
        modelBuilder.Entity<ComplianceConfig>().HasQueryFilter(
            e => _filterTenantId == null || e.TenantId == _filterTenantId);
        modelBuilder.Entity<ConnectorConfig>().HasQueryFilter(
            e => _filterTenantId == null || e.TenantId == _filterTenantId);
        modelBuilder.Entity<WebhookConfig>().HasQueryFilter(
            e => _filterTenantId == null || e.TenantId == _filterTenantId);
        modelBuilder.Entity<ApprovalRequest>().HasQueryFilter(
            e => _filterTenantId == null || e.TenantId == _filterTenantId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(
            e => _filterTenantId == null || e.TenantId == _filterTenantId);

        // Business Document Entities — single TPC root query filter applies to all derived types
        // (CreditNote, DebitNote, PurchaseOrder, DeliveryNote, Reminder).
        // EF Core in TPC requires the filter to be specified on the root entity type, not on each derived type.
        modelBuilder.Entity<DocumentEntity>().HasQueryFilter(
            e => (_filterTenantId == null || e.TenantId == _filterTenantId) && !e.IsDeleted);

        // Document Management Entities
        modelBuilder.Entity<DocumentLine>().HasQueryFilter(
            e => _filterTenantId == null || e.Document.TenantId == _filterTenantId);
        modelBuilder.Entity<DocumentVersionHistory>().HasQueryFilter(
            e => _filterTenantId == null || e.Document.TenantId == _filterTenantId);

        // Cross-tenant safe: show relationships where EITHER source or target belongs to current tenant.
        // (Note: cross-tenant relationships are only created via explicit API call with same-tenant validation.)
        modelBuilder.Entity<DocumentRelationship>().HasQueryFilter(
            e => _filterTenantId == null
              || e.SourceDocument.TenantId == _filterTenantId
              || e.TargetDocument.TenantId == _filterTenantId);

        // Tenant is NOT tenant-filtered (root entity, no FK to itself)
        // InvoiceLine is filtered transitively via Invoice navigation
        // DocumentLines, DocumentVersionHistories, DocumentRelationships are filtered via Document navigation
    }
}
