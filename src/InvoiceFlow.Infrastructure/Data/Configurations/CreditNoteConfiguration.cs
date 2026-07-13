using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.ToTable("credit_notes");

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.Status)
            .IsRequired();

        builder.Property(c => c.DocumentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.IssuerName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(c => c.RecipientName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(c => c.Currency)
            .IsRequired()
            .HasMaxLength(3);

        // Financial columns — decimal(18,2) for currency precision
        builder.Property(c => c.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(c => c.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(c => c.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(c => c.DiscountAmount).HasColumnType("decimal(18,2)");
        builder.Property(c => c.ShippingAmount).HasColumnType("decimal(18,2)");

        builder.Property(c => c.IssuerTaxId).HasMaxLength(50);
        builder.Property(c => c.RecipientTaxId).HasMaxLength(50);
        builder.Property(c => c.CountryCode).HasMaxLength(2);
        builder.Property(c => c.ComplianceId).HasMaxLength(100);
        builder.Property(c => c.OriginalFileName).HasMaxLength(500);
        builder.Property(c => c.StoragePath).HasMaxLength(1000);
        builder.Property(c => c.MimeType).HasMaxLength(100);
        builder.Property(c => c.ReferenceNumber).HasMaxLength(100);
        builder.Property(c => c.ErpId).HasMaxLength(100);
        builder.Property(c => c.Reason).HasMaxLength(500);

        // FK to Tenant
        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to Original Invoice (optional, set null on delete)
        builder.HasOne(c => c.OriginalInvoice)
            .WithMany()
            .HasForeignKey(c => c.OriginalInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Composite indexes for common query patterns
        builder.HasIndex(c => new { c.TenantId, c.DocumentNumber })
            .IsUnique()
            .HasDatabaseName("IX_credit_notes_tenant_number");

        builder.HasIndex(c => new { c.TenantId, c.Status })
            .HasDatabaseName("IX_credit_notes_tenant_status");

        builder.HasIndex(c => new { c.TenantId, c.DocumentDate })
            .HasDatabaseName("IX_credit_notes_tenant_date");

        builder.HasIndex(c => c.OriginalInvoiceId)
            .HasDatabaseName("IX_credit_notes_original_invoice");

        // --- Soft Delete ---
        // IsDeleted defaults to false; query filters exclude deleted rows (see InvoiceFlowDbContext).
        builder.Property(c => c.DeletedAt);
        builder.Property(c => c.DeletedByUserId);
        builder.HasIndex(c => new { c.TenantId, c.IsDeleted })
            .HasDatabaseName("IX_credit_notes_tenant_deleted");
        builder.HasIndex(c => c.DeletedByUserId)
            .HasDatabaseName("IX_credit_notes_deleted_by_user");

        // --- Concurrency Token ---
        // RowVersion is auto-populated by PostgreSQL via xmin or a bytea default.
        builder.Property(c => c.RowVersion)
            .IsRowVersion()
            .HasColumnType("bytea");
    }
}