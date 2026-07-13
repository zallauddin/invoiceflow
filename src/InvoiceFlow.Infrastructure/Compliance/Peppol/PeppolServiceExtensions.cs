using InvoiceFlow.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Compliance.Peppol;

/// <summary>
/// DI extension methods for registering PEPPOL BIS 3.0 compliance services.
/// </summary>
public static class PeppolServiceExtensions
{
    /// <summary>
    /// Registers PEPPOL compliance services, configures <see cref="PeppolAccessPointConfig"/>
    /// from the "Peppol" configuration section, and registers a named HttpClient for Access Point communication.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPeppolCompliance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PeppolAccessPointConfig>(
            configuration.GetSection(PeppolAccessPointConfig.SectionName));

        services.AddHttpClient("PeppolAccessPoint");

        services.AddScoped<IPeppolComplianceService, PeppolComplianceService>();

        return services;
    }
}
