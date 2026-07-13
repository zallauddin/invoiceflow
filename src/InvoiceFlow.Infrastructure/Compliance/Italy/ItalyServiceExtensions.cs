using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Compliance.Italy.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.Italy;

/// <summary>
/// DI extension methods for registering Italian SdI compliance services.
/// </summary>
public static class ItalyServiceExtensions
{
    /// <summary>
    /// Registers the Italian SdI compliance service and binds configuration from the <c>ItalySdi</c> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddItalySdiCompliance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ItalySdiConfig>(configuration.GetSection(ItalySdiConfig.SectionName));

        services.AddSingleton<IItalySdiService>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<ItalySdiConfig>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ItalySdiService>>();
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            return new ItalySdiService(config, httpClient, logger);
        });

        return services;
    }
}
