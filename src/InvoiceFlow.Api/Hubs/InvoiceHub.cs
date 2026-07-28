using Microsoft.AspNetCore.SignalR;

namespace InvoiceFlow.Api.Hubs;

/// <summary>
/// SignalR hub for real-time invoice status updates, dashboard metric pushes,
/// and processing pipeline notifications. Replaces client-side polling.
///
/// Events the server pushes:
///   - "InvoiceStatusChanged"   { invoiceId, tenantId, oldStatus, newStatus }
///   - "DashboardUpdated"       { invoicesToday, successRate, pendingCount, totalProcessed }
///   - "ComplianceUpdated"      { pending, compliant, failed }
///   - "IngestionProgress"      { taskId, fileName, progress, status }
///   - "Notification"           { type, title, message }
///
/// Clients connect to the hub and join their tenant group:
///   this.connection.invoke("JoinTenant", tenantId)
///
/// The hub uses <see cref="IHubContext{InvoiceHub}"/> to push from workers/controllers.
/// </summary>
public class InvoiceHub : Hub
{
    private readonly ILogger<InvoiceHub> _logger;

    public InvoiceHub(ILogger<InvoiceHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects. Extracts tenant_id from the JWT claim
    /// (set by the auth middleware) and joins the tenant notification group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
            _logger.LogDebug(
                "Client {ConnectionId} joined tenant group {TenantId}",
                Context.ConnectionId, tenantId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects. Removes from tenant group.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        }

        if (exception != null)
        {
            _logger.LogWarning(exception,
                "Client {ConnectionId} disconnected with error",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client-invokable: explicitly join a tenant's notification group.
    /// Useful for pages the user navigates to after initial connection.
    /// </summary>
    public async Task JoinTenant(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        _logger.LogDebug(
            "Client {ConnectionId} joined tenant {TenantId} via JoinTenant",
            Context.ConnectionId, tenantId);
    }

    /// <summary>
    /// Client-invokable: leave a tenant's notification group.
    /// </summary>
    public async Task LeaveTenant(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
    }
}

/// <summary>
/// Static helper to send typed messages through the hub from anywhere in the app.
/// Usage: await hubContext.Clients.Group("tenant_...").
///             InvoiceStatusChanged(invoiceId, tenantId, oldStatus, newStatus);
/// </summary>
public static class InvoiceHubExtensions
{
    /// <summary>Pushes an invoice status change to all connected clients in the tenant.</summary>
    public static async Task InvoiceStatusChanged(
        this IHubContext<InvoiceHub> hubContext,
        Guid invoiceId,
        Guid tenantId,
        string oldStatus,
        string newStatus)
    {
        await hubContext.Clients
            .Group($"tenant_{tenantId}")
            .SendAsync("InvoiceStatusChanged", new
            {
                invoiceId = invoiceId.ToString(),
                tenantId = tenantId.ToString(),
                oldStatus,
                newStatus,
                timestamp = DateTime.UtcNow
            });
    }

    /// <summary>Pushes updated dashboard metrics to all connected clients in the tenant.</summary>
    public static async Task DashboardUpdated(
        this IHubContext<InvoiceHub> hubContext,
        Guid tenantId,
        int invoicesToday,
        double successRate,
        int pendingCount,
        int totalProcessed)
    {
        await hubContext.Clients
            .Group($"tenant_{tenantId}")
            .SendAsync("DashboardUpdated", new
            {
                invoicesToday,
                successRate,
                pendingCount,
                totalProcessed,
                timestamp = DateTime.UtcNow
            });
    }

    /// <summary>Pushes updated compliance status to the tenant.</summary>
    public static async Task ComplianceUpdated(
        this IHubContext<InvoiceHub> hubContext,
        Guid tenantId,
        int pending,
        int compliant,
        int failed)
    {
        await hubContext.Clients
            .Group($"tenant_{tenantId}")
            .SendAsync("ComplianceUpdated", new
            {
                pending,
                compliant,
                failed,
                timestamp = DateTime.UtcNow
            });
    }

    /// <summary>Pushes a toast notification to the tenant.</summary>
    public static async Task Notify(
        this IHubContext<InvoiceHub> hubContext,
        Guid tenantId,
        string type,
        string title,
        string message)
    {
        await hubContext.Clients
            .Group($"tenant_{tenantId}")
            .SendAsync("Notification", new
            {
                type,
                title,
                message,
                timestamp = DateTime.UtcNow
            });
    }
}
