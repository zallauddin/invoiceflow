using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>User-specific repository with auth-relevant queries.</summary>
public interface IUserRepository
{
    /// <summary>Finds a user by email within a tenant.</summary>
    Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by ID (bypasses tenant filter).</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a new user.</summary>
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing user.</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Checks if a user with the given email exists in a tenant.</summary>
    Task<bool> ExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);
}
