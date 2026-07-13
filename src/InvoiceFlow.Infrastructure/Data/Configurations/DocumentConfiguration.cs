using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId)
            .IsRequired();

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.OcrText)
            .HasMaxLength(50000); // Large text for OCR output

        builder.Property(d => d.Tags)
            .HasMaxLength(2000);

        builder.Property(d => d.Folder)
            .HasMaxLength(500);

        builder.Property(d => d.Version)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(d => d.IsLatestVersion)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(d => d.Checksum)
            .HasMaxLength(64); // SHA-256 hex string

        builder.Property(d => d.PageCount)
            .IsRequired(false);

        builder.Property(d => d.ThumbnailPath)
            .HasMaxLength(1000);

        // SearchVector is a PostgreSQL tsvector column populated by a raw SQL trigger
        // (see migration 20260711000000_AddSearchVectorGinIndexAndTrigger). The Npgsql
        // provider cannot map a `string` property to `tsvector` directly, and EF never
        // reads/writes this column (DocumentSearchService uses raw SQL), so it is
        // excluded from the EF model. The column still exists in the schema via the
        // raw SQL migration.
        builder.Ignore(d => d.SearchVector);

        builder.Property(d => d.LinkedInvoiceId)
            .IsRequired(false);

        builder.Property(d => d.LinkedCreditNoteId)
            .IsRequired(false);

        builder.Property(d => d.LinkedDebitNoteId)
            .IsRequired(false);

        builder.Property(d => d.LinkedPurchaseOrderId)
            .IsRequired(false);

        builder.Property(d => d.LinkedDeliveryNoteId)
            .IsRequired(false);

        builder.Property(d => d.LinkedReminderId)
            .IsRequired(false);

        builder.Property(d => d.OriginalDocumentId)
            .IsRequired(false);

        // FK to Tenant (no navigation property on Document)
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to Invoice (optional, set null on delete)
        builder.HasOne(d => d.LinkedInvoice)
            .WithMany()
            .HasForeignKey(d => d.LinkedInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to CreditNote (optional, set null on delete)
        builder.HasOne(d => d.LinkedCreditNote)
            .WithMany()
            .HasForeignKey(d => d.LinkedCreditNoteId)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to DebitNote (optional, set null on delete)
        builder.HasOne(d => d.LinkedDebitNote)
            .WithMany()
            .HasForeignKey(d => d.LinkedDebitNoteId)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to PurchaseOrder (optional, set null on delete)
        builder.HasOne(d => d.LinkedPurchaseOrder)
            .WithMany()
            .HasForeignKey(d => d.LinkedPurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to DeliveryNote (optional, set null on delete)
        builder.HasOne(d => d.LinkedDeliveryNote)
            .WithMany()
            .HasForeignKey(d => d.LinkedDeliveryNoteId)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to Reminder (optional, set null on delete)
        builder.HasOne(d => d.LinkedReminder)
            .WithMany()
            .HasForeignKey(d => d.LinkedReminderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Self-referencing FK for document version FK
        builder.HasOne(d => d.OriginalDocument)
            .WithMany(d => d.Versions)
            .HasForeignKey(d => d.OriginalDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index: tenant + document type for filtering
        builder.HasIndex(d => new { d.TenantId, d.DocumentType })
            .HasDatabaseName("IX_documents_tenant_type");

        // Index: tenant + is_latest_version for current version queries
        builder.HasIndex(d => new { d.TenantId, d.IsLatestVersion })
            .HasDatabaseName("IX_documents_tenant_latest");

        // Index: tenant + version for version history queries
        builder.HasIndex(d => new { d.TenantId, d.OriginalDocumentId, d.Version })
            .HasDatabaseName("IX_documents_tenant_original_version");

        // Index: checksum for deduplication
        builder.HasIndex(d => new { d.TenantId, d.Checksum })
            .HasDatabaseName("IX_documents_tenant_checksum")
            .HasFilter("checksum IS NOT NULL");

        // Index: linked entities for reverse lookups
        builder.HasIndex(d => d.LinkedInvoiceId)
            .HasDatabaseName("IX_documents_linked_invoice");

        builder.HasIndex(d => d.LinkedCreditNoteId)
            .HasDatabaseName("IX_documents_linked_credit_note");

        builder.HasIndex(d => d.LinkedDebitNoteId)
            .HasDatabaseName("IX_documents_linked_debit_note");

        builder.HasIndex(d => d.LinkedPurchaseOrderId)
            .HasDatabaseName("IX_documents_linked_purchase_order");

        builder.HasIndex(d => d.LinkedDeliveryNoteId)
            .HasDatabaseName("IX_documents_linked_delivery_note");

        builder.HasIndex(d => d.LinkedReminderId)
            .HasDatabaseName("IX_documents_linked_reminder");

        // Index: OriginalDocumentId for version chains
        builder.HasIndex(d => d.OriginalDocumentId)
            .HasDatabaseName("IX_documents_original_document");

        // GIN index on SearchVector is created via raw SQL migration
        // (see Migrations/20260711000000_AddSearchVectorGinIndexAndTrigger.cs).
        // The Npgsql EF Core provider does not support HasMethod("GIN") on tsvector columns reliably.
    }
}