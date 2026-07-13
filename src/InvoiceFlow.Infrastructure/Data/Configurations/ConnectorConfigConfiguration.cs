using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class ConnectorConfigConfiguration : IEntityTypeConfiguration<ConnectorConfig>
{
    public void Configure(EntityTypeBuilder<ConnectorConfig> builder)
    {
        builder.ToTable("connector_configs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.CredentialsJson)
            .HasMaxLength(10000);

        builder.Property(c => c.ExtraConfigJson)
            .HasMaxLength(10000);

        // FK to Tenant
        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index: tenant + connector type for lookup
        builder.HasIndex(c => new { c.TenantId, c.ConnectorType })
            .HasDatabaseName("IX_connector_configs_tenant_type");
    }
}
