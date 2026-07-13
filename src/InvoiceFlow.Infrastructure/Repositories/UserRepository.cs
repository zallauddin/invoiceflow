using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Repositories;

/// <summary>
/// User-specific repository with auth-relevant queries.
/// Bypasses tenant query filter for cross-tenant lookups (e.g., registration).
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly InvoiceFlowDbContext _context;

    public UserRepository(InvoiceFlowDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default)
    {
        // Uses tenant-scoped query filter (User has global query filter)
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.TenantId == tenantId, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Bypass tenant filter for direct ID lookup
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AnyAsync(u => u.Email == email && u.TenantId == tenantId, cancellationToken);
    }
}
