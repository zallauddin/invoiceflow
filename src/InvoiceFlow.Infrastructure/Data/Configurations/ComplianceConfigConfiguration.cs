using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class ComplianceConfigConfiguration : IEntityTypeConfiguration<ComplianceConfig>
{
    public void Configure(EntityTypeBuilder<ComplianceConfig> builder)
    {
        builder.ToTable("compliance_configs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(c => c.ConfigJson)
            .HasMaxLength(10000);

        // FK to Tenant
        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite unique index: one config per country+model per tenant
        builder.HasIndex(c => new { c.TenantId, c.CountryCode, c.Model })
            .IsUnique()
            .HasDatabaseName("IX_compliance_configs_tenant_country_model");
    }
}
