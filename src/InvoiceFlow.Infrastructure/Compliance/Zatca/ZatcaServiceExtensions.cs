using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Compliance.Zatca.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Compliance.Zatca;

/// <summary>
/// DI extension methods for registering ZATCA e-invoicing compliance services.
/// </summary>
public static class ZatcaServiceExtensions
{
    /// <summary>
    /// Registers the ZATCA compliance service, configures API settings from the "Zatca" configuration section,
    /// and adds a named HttpClient for ZATCA API communication.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration containing the "Zatca" section.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddZatcaCompliance(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ZatcaApiConfig>(configuration.GetSection(ZatcaApiConfig.SectionName));

        services.AddHttpClient("ZatcaApi", (sp, client) =>
        {
            var config = configuration.GetSection(ZatcaApiConfig.SectionName).Get<ZatcaApiConfig>();
            if (config is not null)
            {
                client.BaseAddress = new Uri(config.ApiBaseUrl);
            }

            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IZatcaComplianceService, ZatcaComplianceService>();

        return services;
    }
}
