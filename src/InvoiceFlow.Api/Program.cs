using InvoiceFlow.Api;
using InvoiceFlow.Infrastructure.Data;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog (must be first — replaces default logging) ---
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// --- Register all InvoiceFlow services (DI, EF Core, Auth, CORS, Swagger, Health) ---
builder.Services.AddInvoiceFlowApi(builder.Configuration);

var app = builder.Build();

// --- Middleware pipeline (order matters!) ---

// 1. Global exception handling
app.UseExceptionHandler("/error");

// 2. HTTPS redirection (production)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

// 3. CORS (must be before auth/endpoints)
app.UseCors("InvoiceFlow");

// 4. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 5. Swagger (always available — dev gets it at root, prod at /swagger)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "InvoiceFlow API v1");
    options.RoutePrefix = app.Environment.IsDevelopment() ? "swagger" : string.Empty;
});

// 6. Prometheus metrics endpoint
app.UseHttpMetrics();

// 7. Health checks (unauthenticated)
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

// 8. Prometheus metrics endpoint
app.MapMetrics("/metrics");

// 9. Minimal API endpoints (module registration point)
app.MapEndpoints();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
