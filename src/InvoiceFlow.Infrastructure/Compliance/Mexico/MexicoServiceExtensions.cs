using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Compliance.Mexico.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Compliance.Mexico;

/// <summary>
/// DI extension methods for registering Mexican CFDI compliance services.
/// </summary>
public static class MexicoServiceExtensions
{
    /// <summary>
    /// Registers <see cref="MexicoCfdiService"/> and its configuration from the application configuration.
    /// </summary>
    public static IServiceCollection AddMexicoCfdiCompliance(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MexicoPacConfig>(configuration.GetSection("Compliance:Mexico:Pac"));
        services.AddHttpClient<IMexicoCfdiService, MexicoCfdiService>();
        return services;
    }
}
