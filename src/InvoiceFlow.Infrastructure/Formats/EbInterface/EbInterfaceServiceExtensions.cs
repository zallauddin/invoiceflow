using InvoiceFlow.Formats.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Formats.EbInterface;

/// <summary>DI extension methods for registering ebInterface format reader, writer, and validator.</summary>
public static class EbInterfaceServiceExtensions
{
    /// <summary>
    /// Registers ebInterface format services (reader, writer, validator) as singletons.
    /// </summary>
    public static IServiceCollection AddEbInterfaceFormat(this IServiceCollection services)
    {
        services.AddSingleton<IFormatReader, EbInterfaceFormatReader>();
        services.AddSingleton<IFormatWriter, EbInterfaceFormatWriter>();
        services.AddSingleton<IFormatValidator, EbInterfaceFormatValidator>();
        return services;
    }
}
