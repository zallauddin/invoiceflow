using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Compliance.Brazil.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Compliance.Brazil;

/// <summary>
/// DI extension methods for registering Brazilian NF-e compliance services.
/// </summary>
public static class BrazilServiceExtensions
{
    /// <summary>
    /// Registers <see cref="BrazilNfeService"/> and its configuration from the application configuration.
    /// </summary>
    public static IServiceCollection AddBrazilNfeCompliance(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BrazilSefazConfig>(configuration.GetSection("Compliance:Brazil:Sefaz"));
        services.AddHttpClient<IBrazilNfeService, BrazilNfeService>();
        return services;
    }
}
