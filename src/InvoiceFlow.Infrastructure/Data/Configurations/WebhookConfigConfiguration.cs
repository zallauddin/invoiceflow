using System.Text.Json;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class WebhookConfigConfiguration : IEntityTypeConfiguration<WebhookConfig>
{
    public void Configure(EntityTypeBuilder<WebhookConfig> builder)
    {
        builder.ToTable("webhook_configs");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.TenantId)
            .IsRequired();

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(w => w.Url)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(w => w.Secret)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        // Events stored as JSON array of WebhookEventType enum values
        builder.Property(w => w.Events)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<WebhookEventType>>(v, (JsonSerializerOptions?)null) ?? new List<WebhookEventType>())
            .HasColumnType("jsonb");

        // FK to Tenant
        builder.HasOne(w => w.Tenant)
            .WithMany()
            .HasForeignKey(w => w.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index: webhook name per tenant
        builder.HasIndex(w => new { w.TenantId, w.Name })
            .IsUnique()
            .HasDatabaseName("IX_webhook_configs_tenant_name");
    }
}
