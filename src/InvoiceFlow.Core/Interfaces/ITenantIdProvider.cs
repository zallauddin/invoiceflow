namespace InvoiceFlow.Core.Interfaces;

/// <summary>Provides the current tenant identifier for multi-tenant query filtering.</summary>
public interface ITenantIdProvider
{
    /// <summary>The current tenant ID, or null if running in a tenant-agnostic context (e.g., system admin).</summary>
    Guid? TenantId { get; }
}
