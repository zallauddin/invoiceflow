using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.DTOs.Auth;

/// <summary>User information returned after authentication.</summary>
public sealed record UserDto
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required UserRole Role { get; init; }
    public required bool IsActive { get; init; }
}
