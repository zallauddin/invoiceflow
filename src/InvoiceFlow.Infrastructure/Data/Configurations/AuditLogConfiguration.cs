using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
            .IsRequired();

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.PerformedBy)
            .HasMaxLength(100);

        builder.Property(a => a.Details)
            .HasMaxLength(10000);

        builder.Property(a => a.PreviousHash)
            .HasMaxLength(64); // SHA-256 hex string

        builder.Property(a => a.CurrentHash)
            .HasMaxLength(64); // SHA-256 hex string

        // FK to Invoice (optional, set null on delete)
        builder.HasOne(a => a.Invoice)
            .WithMany()
            .HasForeignKey(a => a.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index: tenant + action for filtering
        builder.HasIndex(a => new { a.TenantId, a.Action })
            .HasDatabaseName("IX_audit_logs_tenant_action");

        // Index: created_at for chronological queries and retention cleanup
        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("IX_audit_logs_created_at");
    }
}
