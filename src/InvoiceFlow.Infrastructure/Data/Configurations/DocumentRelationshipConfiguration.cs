using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class DocumentRelationshipConfiguration : IEntityTypeConfiguration<DocumentRelationship>
{
    public void Configure(EntityTypeBuilder<DocumentRelationship> builder)
    {
        builder.ToTable("document_relationships");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.SourceDocumentId)
            .IsRequired();

        builder.Property(r => r.TargetDocumentId)
            .IsRequired();

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.RelationshipType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.Property(r => r.Metadata)
            .HasMaxLength(10000);

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(100);

        builder.Property(r => r.CreatedByUserId);

        builder.Property(r => r.UpdatedAt);

        builder.Property(r => r.TargetTenantId);

        // Restrict: caller must delete relationships before deleting the document — prevents orphaned relationship rows.
        builder.HasOne(r => r.SourceDocument)
            .WithMany(d => d.OutgoingRelationships)
            .HasForeignKey(r => r.SourceDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: caller must delete relationships before deleting the document — prevents orphaned relationship rows.
        builder.HasOne(r => r.TargetDocument)
            .WithMany(d => d.IncomingRelationships)
            .HasForeignKey(r => r.TargetDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to Tenant
        builder.HasOne(r => r.Tenant)
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to User (nullable — SetNull valid here)
        builder.HasOne(r => r.CreatedByUser)
            .WithMany()
            .HasForeignKey(r => r.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to TargetTenant (Restrict — target tenant shouldn't be deleted while referenced)
        builder.HasOne(r => r.TargetTenant)
            .WithMany()
            .HasForeignKey(r => r.TargetTenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint: one relationship of same type between same pair
        builder.HasIndex(r => new { r.SourceDocumentId, r.TargetDocumentId, r.RelationshipType })
            .IsUnique()
            .HasDatabaseName("IX_document_relationships_source_target_type");

        // Composite indexes for common query patterns
        builder.HasIndex(r => new { r.TenantId, r.SourceDocumentId })
            .HasDatabaseName("IX_document_relationships_tenant_source");

        builder.HasIndex(r => new { r.TenantId, r.TargetDocumentId })
            .HasDatabaseName("IX_document_relationships_tenant_target");

        builder.HasIndex(r => r.RelationshipType)
            .HasDatabaseName("IX_document_relationships_type");

        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_document_relationships_created_at");

        builder.HasIndex(r => r.CreatedByUserId)
            .HasDatabaseName("IX_document_relationships_created_by_user");
    }
}