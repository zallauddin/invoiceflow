namespace InvoiceFlow.Api.Middleware;

/// <summary>
/// API versioning middleware that rewrites incoming request paths to strip
/// version prefixes (e.g., /api/v1/invoices → /api/invoices) and adds
/// version information to response headers.
///
/// This allows the backend to serve multiple API versions simultaneously.
/// When a new version is needed, the middleware routes old versions to
/// backward-compatible handlers by rewriting the path.
///
/// Version discovery:
///   GET /api/versions → returns list of supported versions
///
/// Usage in Program.cs:
///   app.UseMiddleware<ApiVersioningMiddleware>();
///   (Place BEFORE authentication/endpoint middleware)
/// </summary>
public class ApiVersioningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiVersioningMiddleware> _logger;

    /// <summary>
    /// Supported API versions. When adding a new version, list it here
    /// and create the corresponding route handlers.
    /// </summary>
    private static readonly HashSet<string> SupportedVersions = new()
    {
        "v1"
    };

    /// <summary>
    /// Default version used when no version is specified in the URL.
    /// </summary>
    private const string DefaultVersion = "v1";

    public ApiVersioningMiddleware(RequestDelegate next, ILogger<ApiVersioningMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip versioning for non-API paths
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Extract version from URL path: /api/v1/invoices → version=v1, remaining=/api/invoices
        var version = DefaultVersion;
        var hasVersionInPath = false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // segments[0] == "api"
        if (segments.Length >= 3 && segments[1].Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            var potentialVersion = segments[2].ToLowerInvariant();
            if (potentialVersion.StartsWith('v') && potentialVersion.Length > 1 &&
                int.TryParse(potentialVersion[1..], out _))
            {
                version = SupportedVersions.Contains(potentialVersion) ? potentialVersion : DefaultVersion;
                hasVersionInPath = true;

                // Rewrite the path: remove the version segment
                var remainingPath = string.Join("/", segments.Skip(1).Take(1)) + "/" +
                                    string.Join("/", segments.Skip(3));
                context.Request.Path = "/" + remainingPath;

                _logger.LogDebug(
                    "API versioning: Rewrote {OriginalPath} → {NewPath} (version {Version})",
                    path, context.Request.Path, version);
            }
        }

        // Add version info to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-API-Version"] = version;
            context.Response.Headers["X-API-Default-Version"] = DefaultVersion;
            context.Response.Headers["X-API-Supported-Versions"] = string.Join(", ", SupportedVersions);
            return Task.CompletedTask;
        });

        // Handle version discovery endpoint
        if (path.Equals("/api/versions", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/v1/versions", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    defaultVersion = DefaultVersion,
                    supportedVersions = SupportedVersions,
                    currentVersion = version,
                    deprecationPolicy = new
                    {
                        minimumSupportedVersion = "v1",
                        deprecationNoticePeriod = "6 months",
                        migrationGuide = "/docs/api-migration"
                    }
                }));
            return;
        }

        // Add deprecation warning header for old versions
        if (hasVersionInPath && version != DefaultVersion)
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["Warning"] =
                    $"299 - \"API version {version} is deprecated. Use {DefaultVersion} instead.\"";
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }
}
