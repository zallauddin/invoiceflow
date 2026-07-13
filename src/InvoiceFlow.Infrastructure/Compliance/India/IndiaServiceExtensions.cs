using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Compliance.India.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Compliance.India;

/// <summary>
/// DI extension methods for registering Indian IRP e-Invoice compliance services.
/// </summary>
public static class IndiaServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IndiaIrpService"/> and its configuration from the application configuration.
    /// </summary>
    public static IServiceCollection AddIndiaIrpCompliance(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IndiaIrpConfig>(configuration.GetSection("Compliance:India:Irp"));
        services.AddHttpClient<IIndiaIrpService, IndiaIrpService>();
        return services;
    }
}
