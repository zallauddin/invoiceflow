using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId)
            .IsRequired();

        builder.Property(i => i.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.VendorName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(i => i.BuyerName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(i => i.Currency)
            .IsRequired()
            .HasMaxLength(3);

        // Financial columns — decimal(18,2) for currency precision
        builder.Property(i => i.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(i => i.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(i => i.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(i => i.DiscountAmount).HasColumnType("decimal(18,2)");
        builder.Property(i => i.ShippingAmount).HasColumnType("decimal(18,2)");

        builder.Property(i => i.VendorTaxId).HasMaxLength(50);
        builder.Property(i => i.BuyerTaxId).HasMaxLength(50);
        builder.Property(i => i.CountryCode).HasMaxLength(2);
        builder.Property(i => i.ComplianceId).HasMaxLength(100);
        builder.Property(i => i.OriginalFileName).HasMaxLength(500);
        builder.Property(i => i.StoragePath).HasMaxLength(1000);
        builder.Property(i => i.MimeType).HasMaxLength(100);
        builder.Property(i => i.ReferenceNumber).HasMaxLength(100);
        builder.Property(i => i.ErpId).HasMaxLength(100);

        // FK to Tenant
        builder.HasOne(i => i.Tenant)
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite indexes for common query patterns
        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber })
            .IsUnique()
            .HasDatabaseName("IX_invoices_tenant_number");

        builder.HasIndex(i => new { i.TenantId, i.Status })
            .HasDatabaseName("IX_invoices_tenant_status");

        builder.HasIndex(i => new { i.TenantId, i.InvoiceDate })
            .HasDatabaseName("IX_invoices_tenant_date");
    }
}
