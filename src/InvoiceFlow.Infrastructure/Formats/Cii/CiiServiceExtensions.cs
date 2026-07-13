using InvoiceFlow.Formats.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Formats.Cii;

/// <summary>DI extension methods for registering CII format reader and validator services.</summary>
public static class CiiServiceExtensions
{
    /// <summary>
    /// Registers CII format services (reader, validator) as singletons.
    /// CII does not have a writer — it is a read-only format in this implementation.
    /// </summary>
    public static IServiceCollection AddCiiFormat(this IServiceCollection services)
    {
        services.AddSingleton<IFormatReader, CiiFormatReader>();
        services.AddSingleton<IFormatValidator, CiiFormatValidator>();
        return services;
    }
}
