using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class DocumentEntityConfiguration : IEntityTypeConfiguration<DocumentEntity>
{
    public void Configure(EntityTypeBuilder<DocumentEntity> builder)
    {
        // Key must be configured on the root abstract type for TPC inheritance.
        builder.HasKey(e => e.Id);

        // TPC: Table-Per-Concrete-Type — each derived type maps to its own table
        // containing all columns (inherited + own). No shared table for the abstract base.
        builder.UseTpcMappingStrategy();

        // Reminder hides Status with 'new ReminderStatus Status' (different type from
        // the base 'DocumentStatus Status'). TPC cannot reconcile two properties with the
        // same name but different CLR types. Ignore on the base; each derived type
        // configures its own mapping explicitly.
        builder.Ignore(e => e.Status);

        // DomainEvents is not persisted — exclude it at the root type so TPC
        // derived types inherit the ignore (can't ignore on derived types in TPC).
        builder.Ignore(e => e.DomainEvents);
    }
}
