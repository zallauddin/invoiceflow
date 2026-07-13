using InvoiceFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Data.Configurations;

public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("approval_requests");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.InvoiceId)
            .IsRequired();

        builder.Property(a => a.TenantId)
            .IsRequired();

        builder.Property(a => a.Comments)
            .HasMaxLength(2000);

        // FK to Invoice (cascade delete — approval goes with invoice)
        builder.HasOne(a => a.Invoice)
            .WithMany()
            .HasForeignKey(a => a.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Tenant (no navigation property on ApprovalRequest)
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to assigned user (optional)
        builder.HasOne(a => a.AssignedToUser)
            .WithMany()
            .HasForeignKey(a => a.AssignedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to reviewing user (optional)
        builder.HasOne(a => a.ReviewedByUser)
            .WithMany()
            .HasForeignKey(a => a.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index: tenant + status for filtering pending approvals
        builder.HasIndex(a => new { a.TenantId, a.Status })
            .HasDatabaseName("IX_approval_requests_tenant_status");
    }
}
