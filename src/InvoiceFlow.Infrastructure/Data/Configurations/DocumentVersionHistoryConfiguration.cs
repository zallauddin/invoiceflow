using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class DocumentVersionHistoryConfiguration : IEntityTypeConfiguration<DocumentVersionHistory>
{
    public void Configure(EntityTypeBuilder<DocumentVersionHistory> builder)
    {
        builder.ToTable("document_version_histories");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.DocumentId)
            .IsRequired();

        builder.Property(v => v.TenantId)
            .IsRequired();

        builder.Property(v => v.Version)
            .IsRequired();

        builder.Property(v => v.ChangeType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(v => v.Description)
            .HasMaxLength(500);

        builder.Property(v => v.ChangeDetails)
            .HasMaxLength(10000);

        builder.Property(v => v.ChangedBy)
            .HasMaxLength(100);

        builder.Property(v => v.OldValue)
            .IsRequired(false);

        builder.Property(v => v.NewValue)
            .IsRequired(false);

        builder.Property(v => v.FieldName)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(v => v.ChangedByUserId)
            .IsRequired(false);

        // FK to User who made the change
        builder.HasOne(v => v.ChangedByUser)
            .WithMany()
            .HasForeignKey(v => v.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to Document with cascade delete
        builder.HasOne(v => v.Document)
            .WithMany(d => d.VersionHistory)
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Tenant
        builder.HasOne(v => v.Tenant)
            .WithMany()
            .HasForeignKey(v => v.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite indexes for common query patterns
        builder.HasIndex(v => new { v.DocumentId, v.Version })
            .IsUnique()
            .HasDatabaseName("IX_document_version_histories_document_version");

        builder.HasIndex(v => new { v.TenantId, v.DocumentId })
            .HasDatabaseName("IX_document_version_histories_tenant_document");

        builder.HasIndex(v => v.CreatedAt)
            .HasDatabaseName("IX_document_version_histories_created_at");

        builder.HasIndex(v => v.ChangeType)
            .HasDatabaseName("IX_document_version_histories_change_type");

        builder.HasIndex(v => new { v.DocumentId, v.FieldName })
            .HasDatabaseName("IX_document_version_histories_document_field");
    }
}