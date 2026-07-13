using InvoiceFlow.Formats.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Formats.XRechnung;

/// <summary>DI extension methods for registering XRechnung format reader, writer, and validator.</summary>
public static class XRechnungServiceExtensions
{
    /// <summary>
    /// Registers XRechnung 3.0 format services (reader, writer, validator) as singletons.
    /// </summary>
    public static IServiceCollection AddXRechnungFormat(this IServiceCollection services)
    {
        services.AddSingleton<IFormatReader, XRechnungFormatReader>();
        services.AddSingleton<IFormatWriter, XRechnungFormatWriter>();
        services.AddSingleton<IFormatValidator, XRechnungFormatValidator>();
        return services;
    }
}
