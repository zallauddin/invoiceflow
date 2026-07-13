using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class DeliveryNoteConfiguration : IEntityTypeConfiguration<DeliveryNote>
{
    public void Configure(EntityTypeBuilder<DeliveryNote> builder)
    {
        builder.ToTable("delivery_notes");

        builder.Property(d => d.TenantId)
            .IsRequired();

        builder.Property(d => d.Status)
            .IsRequired();

        builder.Property(d => d.DocumentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.IssuerName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(d => d.RecipientName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(d => d.Currency)
            .IsRequired()
            .HasMaxLength(3);

        // Financial columns — decimal(18,2) for currency precision
        builder.Property(d => d.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(d => d.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(d => d.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(d => d.DiscountAmount).HasColumnType("decimal(18,2)");
        builder.Property(d => d.ShippingAmount).HasColumnType("decimal(18,2)");

        builder.Property(d => d.IssuerTaxId).HasMaxLength(50);
        builder.Property(d => d.RecipientTaxId).HasMaxLength(50);
        builder.Property(d => d.CountryCode).HasMaxLength(2);
        builder.Property(d => d.ComplianceId).HasMaxLength(100);
        builder.Property(d => d.OriginalFileName).HasMaxLength(500);
        builder.Property(d => d.StoragePath).HasMaxLength(1000);
        builder.Property(d => d.MimeType).HasMaxLength(100);
        builder.Property(d => d.ReferenceNumber).HasMaxLength(100);
        builder.Property(d => d.ErpId).HasMaxLength(100);
        builder.Property(d => d.DeliveryAddress).HasMaxLength(2000);
        builder.Property(d => d.CarrierName).HasMaxLength(300);
        builder.Property(d => d.TrackingNumber).HasMaxLength(100);
        builder.Property(d => d.ReceivedBy).HasMaxLength(300);

        // Delivery note-specific property configs
        builder.Property(d => d.DeliveredQuantity).HasColumnType("decimal(18,2)").IsRequired(false);
        builder.Property(d => d.SignaturePath).HasMaxLength(1000).IsRequired(false);
        builder.Property(d => d.ProofOfDeliveryPath).HasMaxLength(1000).IsRequired(false);
        builder.Property(d => d.ReceivedAt).IsRequired(false);
        builder.Property(d => d.ReceiverSignature).IsRequired(false);

        // FK to Tenant
        builder.HasOne(d => d.Tenant)
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to Purchase Order (optional, set null on delete)
        builder.HasOne(d => d.PurchaseOrder)
            .WithMany()
            .HasForeignKey(d => d.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Composite indexes for common query patterns
        builder.HasIndex(d => new { d.TenantId, d.DocumentNumber })
            .IsUnique()
            .HasDatabaseName("IX_delivery_notes_tenant_number");

        builder.HasIndex(d => new { d.TenantId, d.Status })
            .HasDatabaseName("IX_delivery_notes_tenant_status");

        builder.HasIndex(d => new { d.TenantId, d.DocumentDate })
            .HasDatabaseName("IX_delivery_notes_tenant_date");

        builder.HasIndex(d => d.PurchaseOrderId)
            .HasDatabaseName("IX_delivery_notes_purchase_order");

        builder.HasIndex(d => d.TrackingNumber)
            .HasDatabaseName("IX_delivery_notes_tracking");

        // --- Soft Delete ---
        // IsDeleted defaults to false; query filters exclude deleted rows (see InvoiceFlowDbContext).
        builder.Property(d => d.DeletedAt);
        builder.Property(d => d.DeletedByUserId);
        builder.HasIndex(d => new { d.TenantId, d.IsDeleted })
            .HasDatabaseName("IX_delivery_notes_tenant_deleted");
        builder.HasIndex(d => d.DeletedByUserId)
            .HasDatabaseName("IX_delivery_notes_deleted_by_user");

        // --- Concurrency Token ---
        // RowVersion is auto-populated by PostgreSQL via xmin or a bytea default.
        builder.Property(d => d.RowVersion)
            .IsRowVersion()
            .HasColumnType("bytea");
    }
}