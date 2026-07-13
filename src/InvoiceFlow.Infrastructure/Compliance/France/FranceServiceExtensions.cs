using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Compliance.France.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.France;

/// <summary>
/// DI extension methods for registering French PPF compliance services.
/// </summary>
public static class FranceServiceExtensions
{
    /// <summary>
    /// Registers the French PPF compliance service and binds configuration from the <c>FrancePpf</c> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFrancePpfCompliance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FrancePpfConfig>(configuration.GetSection(FrancePpfConfig.SectionName));

        services.AddSingleton<IFrancePpfService>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<FrancePpfConfig>>();
            var logger = sp.GetRequiredService<ILogger<FrancePpfService>>();
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            return new FrancePpfService(config, httpClient, logger);
        });

        return services;
    }
}
