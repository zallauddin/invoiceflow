using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("invoice_lines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.InvoiceId)
            .IsRequired();

        builder.Property(l => l.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(l => l.ProductCode).HasMaxLength(100);
        builder.Property(l => l.HsnCode).HasMaxLength(20);
        builder.Property(l => l.Unit).HasMaxLength(20);
        builder.Property(l => l.TaxCategory).HasMaxLength(10);

        // Financial columns — decimal(18,2)
        builder.Property(l => l.Quantity).HasColumnType("decimal(18,2)");
        builder.Property(l => l.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(l => l.LineTotal).HasColumnType("decimal(18,2)");
        builder.Property(l => l.TaxRate).HasColumnType("decimal(18,2)");
        builder.Property(l => l.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(l => l.DiscountPercent).HasColumnType("decimal(18,2)");
        builder.Property(l => l.DiscountAmount).HasColumnType("decimal(18,2)");

        // FK to Invoice with cascade delete
        builder.HasOne(l => l.Invoice)
            .WithMany(i => i.Lines)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite unique index: one line number per invoice
        builder.HasIndex(l => new { l.InvoiceId, l.LineNumber })
            .IsUnique()
            .HasDatabaseName("IX_invoice_lines_invoice_linenum");

        // No global query filter — filtered transitively via Invoice
    }
}
