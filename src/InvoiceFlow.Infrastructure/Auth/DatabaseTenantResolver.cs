using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Auth;

/// <summary>
/// Resolves tenants by slug or ID using the database.
/// Used by AuthService to validate tenant existence during login/registration.
/// </summary>
public class DatabaseTenantResolver : ITenantResolver
{
    private readonly InvoiceFlowDbContext _context;

    public DatabaseTenantResolver(InvoiceFlowDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .IgnoreQueryFilters() // Tenant is root entity — query filter doesn't apply but being explicit
            .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }
}
