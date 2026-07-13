using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.Property(p => p.Status)
            .IsRequired();

        builder.Property(p => p.DocumentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.IssuerName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.RecipientName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3);

        // Financial columns — decimal(18,2) for currency precision
        builder.Property(p => p.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(p => p.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.DiscountAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.ShippingAmount).HasColumnType("decimal(18,2)");

        builder.Property(p => p.IssuerTaxId).HasMaxLength(50);
        builder.Property(p => p.RecipientTaxId).HasMaxLength(50);
        builder.Property(p => p.CountryCode).HasMaxLength(2);
        builder.Property(p => p.ComplianceId).HasMaxLength(100);
        builder.Property(p => p.OriginalFileName).HasMaxLength(500);
        builder.Property(p => p.StoragePath).HasMaxLength(1000);
        builder.Property(p => p.MimeType).HasMaxLength(100);
        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);
        builder.Property(p => p.ErpId).HasMaxLength(100);
        builder.Property(p => p.DeliveryAddress).HasMaxLength(2000);
        builder.Property(p => p.PaymentTerms).HasMaxLength(100);
        builder.Property(p => p.Incoterms).HasMaxLength(20);

        // Purchase order-specific property configs
        builder.Property(p => p.ShipToName).HasMaxLength(300).IsRequired(false);
        builder.Property(p => p.ShipToAddress).IsRequired(false);
        builder.Property(p => p.BillToName).HasMaxLength(300).IsRequired(false);
        builder.Property(p => p.BillToAddress).IsRequired(false);
        builder.Property(p => p.ContactName).HasMaxLength(300).IsRequired(false);
        builder.Property(p => p.ContactEmail).HasMaxLength(255).IsRequired(false);
        builder.Property(p => p.ContactPhone).HasMaxLength(50).IsRequired(false);

        // FK to Tenant
        builder.HasOne(p => p.Tenant)
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite indexes for common query patterns
        builder.HasIndex(p => new { p.TenantId, p.DocumentNumber })
            .IsUnique()
            .HasDatabaseName("IX_purchase_orders_tenant_number");

        builder.HasIndex(p => new { p.TenantId, p.Status })
            .HasDatabaseName("IX_purchase_orders_tenant_status");

        builder.HasIndex(p => new { p.TenantId, p.DocumentDate })
            .HasDatabaseName("IX_purchase_orders_tenant_date");

        builder.HasIndex(p => p.ExpectedDeliveryDate)
            .HasDatabaseName("IX_purchase_orders_expected_delivery");

        // --- Soft Delete ---
        // IsDeleted defaults to false; query filters exclude deleted rows (see InvoiceFlowDbContext).
        builder.Property(p => p.DeletedAt);
        builder.Property(p => p.DeletedByUserId);
        builder.HasIndex(p => new { p.TenantId, p.IsDeleted })
            .HasDatabaseName("IX_purchase_orders_tenant_deleted");
        builder.HasIndex(p => p.DeletedByUserId)
            .HasDatabaseName("IX_purchase_orders_deleted_by_user");

        // --- Concurrency Token ---
        // RowVersion is auto-populated by PostgreSQL via xmin or a bytea default.
        builder.Property(p => p.RowVersion)
            .IsRowVersion()
            .HasColumnType("bytea");
    }
}