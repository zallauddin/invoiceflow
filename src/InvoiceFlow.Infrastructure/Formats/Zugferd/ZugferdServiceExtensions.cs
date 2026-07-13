using InvoiceFlow.Formats.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Formats.Zugferd;

/// <summary>DI extension methods for registering ZUGFeRD/Factur-X format reader, writer, and validator.</summary>
public static class ZugferdServiceExtensions
{
    /// <summary>
    /// Registers ZUGFeRD/Factur-X format services (reader, writer, validator) as singletons.
    /// </summary>
    public static IServiceCollection AddZugferdFormat(this IServiceCollection services)
    {
        services.AddSingleton<IFormatReader, ZugferdFormatReader>();
        services.AddSingleton<IFormatWriter, ZugferdFormatWriter>();
        services.AddSingleton<IFormatValidator, ZugferdFormatValidator>();
        return services;
    }
}
