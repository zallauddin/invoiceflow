using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>Resolves tenants by slug or ID for auth operations.</summary>
public interface ITenantResolver
{
    /// <summary>Finds a tenant by its URL slug.</summary>
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Finds a tenant by its ID.</summary>
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
