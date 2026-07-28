namespace InvoiceFlow.Api.Authorization;

/// <summary>
/// Role constants used across the application for RBAC policy enforcement.
/// Matches the <see cref="InvoiceFlow.Core.Enums.UserRole"/> enum values.
/// </summary>
public static class RbacRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string Viewer = "Viewer";
}

/// <summary>
/// Policy name constants for the authorization policies registered in DependencyInjection.
/// </summary>
public static class RbacPolicies
{
    /// <summary>Full system access — admin only.</summary>
    public const string RequireAdmin = "RequireAdmin";

    /// <summary>Can approve/reject invoices — Admin and User roles.</summary>
    public const string RequireApprover = "RequireApprover";

    /// <summary>Read-only access — all authenticated users.</summary>
    public const string RequireViewer = "RequireViewer";

    /// <summary>Can run compliance processing — Admin and User roles.</summary>
    public const string RequireComplianceAccess = "RequireComplianceAccess";

    /// <summary>Can manage ERP connectors — Admin only.</summary>
    public const string RequireConnectorManagement = "RequireConnectorManagement";

    /// <summary>Can manage tenant settings and users — Admin only.</summary>
    public const string RequireTenantAdmin = "RequireTenantAdmin";
}
