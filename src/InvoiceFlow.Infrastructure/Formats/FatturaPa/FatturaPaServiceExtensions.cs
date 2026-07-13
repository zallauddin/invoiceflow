using InvoiceFlow.Formats.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa;

/// <summary>DI extension methods for registering FatturaPA format reader, writer, and validator.</summary>
public static class FatturaPaServiceExtensions
{
    /// <summary>
    /// Registers FatturaPA v1.2 format services (reader, writer, validator) as singletons.
    /// </summary>
    public static IServiceCollection AddFatturaPaFormat(this IServiceCollection services)
    {
        services.AddSingleton<IFormatReader, FatturaPaFormatReader>();
        services.AddSingleton<IFormatWriter, FatturaPaFormatWriter>();
        services.AddSingleton<IFormatValidator, FatturaPaFormatValidator>();
        return services;
    }
}
