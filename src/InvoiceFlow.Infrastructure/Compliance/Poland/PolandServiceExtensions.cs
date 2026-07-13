using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Compliance.Poland.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.Poland;

/// <summary>
/// DI extension methods for registering Polish KSeF compliance services.
/// </summary>
public static class PolandServiceExtensions
{
    /// <summary>
    /// Registers the Polish KSeF compliance service and binds configuration from the <c>PolandKsef</c> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPolandKsefCompliance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PolandKsefConfig>(configuration.GetSection(PolandKsefConfig.SectionName));

        services.AddSingleton<IPolandKsefService>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<PolandKsefConfig>>();
            var logger = sp.GetRequiredService<ILogger<PolandKsefService>>();
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            return new PolandKsefService(config, httpClient, logger);
        });

        return services;
    }
}
