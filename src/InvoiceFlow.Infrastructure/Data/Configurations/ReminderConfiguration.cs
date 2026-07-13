using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders");

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.Status)
            .IsRequired();

        builder.Property(r => r.DocumentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.IssuerName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(r => r.RecipientName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(r => r.Currency)
            .IsRequired()
            .HasMaxLength(3);

        // Financial columns — decimal(18,2) for currency precision
        builder.Property(r => r.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(r => r.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(r => r.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(r => r.DiscountAmount).HasColumnType("decimal(18,2)");
        builder.Property(r => r.ShippingAmount).HasColumnType("decimal(18,2)");

        builder.Property(r => r.IssuerTaxId).HasMaxLength(50);
        builder.Property(r => r.RecipientTaxId).HasMaxLength(50);
        builder.Property(r => r.CountryCode).HasMaxLength(2);
        builder.Property(r => r.ComplianceId).HasMaxLength(100);
        builder.Property(r => r.OriginalFileName).HasMaxLength(500);
        builder.Property(r => r.StoragePath).HasMaxLength(1000);
        builder.Property(r => r.MimeType).HasMaxLength(100);
        builder.Property(r => r.ReferenceNumber).HasMaxLength(100);
        builder.Property(r => r.ErpId).HasMaxLength(100);

        // FK to Tenant
        builder.HasOne(r => r.Tenant)
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to Invoice (cascade delete — reminder goes with invoice)
        builder.HasOne(r => r.Invoice)
            .WithMany()
            .HasForeignKey(r => r.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite indexes for common query patterns
        builder.HasIndex(r => new { r.TenantId, r.DocumentNumber })
            .IsUnique()
            .HasDatabaseName("IX_reminders_tenant_number");

        builder.HasIndex(r => new { r.TenantId, r.Status })
            .HasDatabaseName("IX_reminders_tenant_status");

        builder.HasIndex(r => new { r.TenantId, r.DocumentDate })
            .HasDatabaseName("IX_reminders_tenant_date");

        builder.HasIndex(r => r.InvoiceId)
            .HasDatabaseName("IX_reminders_invoice");

        builder.HasIndex(r => r.ReminderLevel)
            .HasDatabaseName("IX_reminders_level");

        // Reminder-specific property configs
        builder.Property(r => r.DueDate).IsRequired(false);
        builder.Property(r => r.SentAt).IsRequired(false);
        builder.Property(r => r.AcknowledgedAt).IsRequired(false);

        builder.HasIndex(r => r.DueDate)
            .HasDatabaseName("IX_reminders_due_date");

        // --- Soft Delete ---
        // IsDeleted defaults to false; query filters exclude deleted rows (see InvoiceFlowDbContext).
        builder.Property(r => r.DeletedAt);
        builder.Property(r => r.DeletedByUserId);
        builder.HasIndex(r => new { r.TenantId, r.IsDeleted })
            .HasDatabaseName("IX_reminders_tenant_deleted");
        builder.HasIndex(r => r.DeletedByUserId)
            .HasDatabaseName("IX_reminders_deleted_by_user");

        // --- Concurrency Token ---
        // RowVersion is auto-populated by PostgreSQL via xmin or a bytea default.
        builder.Property(r => r.RowVersion)
            .IsRowVersion()
            .HasColumnType("bytea");
    }
}