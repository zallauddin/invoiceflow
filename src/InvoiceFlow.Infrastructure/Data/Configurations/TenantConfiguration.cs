using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100);

        // Unique index on Slug (URL-safe routing key)
        builder.HasIndex(t => t.Slug)
            .IsUnique();

        builder.Property(t => t.TaxId)
            .HasMaxLength(50);

        builder.Property(t => t.Country)
            .HasMaxLength(2);

        // Tenant is the root entity — no global query filter
    }
}
