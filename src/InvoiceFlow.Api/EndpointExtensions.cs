using InvoiceFlow.Api.Endpoints;

namespace InvoiceFlow.Api;

/// <summary>
/// Extension method to register all minimal API endpoint groups.
/// Keeps Program.cs clean — each module adds its endpoints here.
/// </summary>
public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        // Module registration point — each feature module adds its endpoints here
        app.MapAuthEndpoints();
        app.MapIngestionEndpoints();
        app.MapCreditNoteEndpoints();
        app.MapDebitNoteEndpoints();
        app.MapPurchaseOrderEndpoints();
        app.MapDeliveryNoteEndpoints();
        app.MapReminderEndpoints();
        app.MapDocumentsEndpoints();

        // Base info endpoint
        app.MapGet("/", () => Results.Ok(new
        {
            name = "InvoiceFlow API",
            version = "v1",
            environment = app.Environment.EnvironmentName,
            timestamp = DateTime.UtcNow
        })).WithName("Root")
          .WithTags("System")
          .AllowAnonymous();

        return app;
    }
}
