using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Cii;
using InvoiceFlow.Infrastructure.Formats.EbInterface;
using InvoiceFlow.Infrastructure.Formats.FatturaPa;
using InvoiceFlow.Infrastructure.Formats.Ubl21;
using InvoiceFlow.Infrastructure.Formats.XRechnung;
using InvoiceFlow.Infrastructure.Formats.Zugferd;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Formats.Conversion;

/// <summary>
/// DI extension methods for registering all known format services and the conversion pipeline.
/// </summary>
public static class FormatRegistrationServiceExtensions
{
    /// <summary>
    /// Registers all known format readers, writers, validators, and the conversion pipeline.
    /// </summary>
    public static IServiceCollection AddAllFormats(this IServiceCollection services)
    {
        // Core conversion services
        services.AddFormatConversion();

        // Individual format registrations
        services.AddUbl21Format();
        services.AddXRechnungFormat();
        services.AddZugferdFormat();
        services.AddFatturaPaFormat();
        services.AddEbInterfaceFormat();
        services.AddCiiFormat();

        // Hosted initializer to populate registry descriptors at startup
        services.AddHostedService<FormatRegistryInitializer>();

        return services;
    }
}
