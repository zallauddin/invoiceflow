using InvoiceFlow.Formats.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21;

/// <summary>DI extension methods for registering UBL 2.1 format reader, writer, and validator.</summary>
public static class Ubl21ServiceExtensions
{
    /// <summary>
    /// Registers UBL 2.1 format services (reader, writer, validator) as singletons.
    /// </summary>
    public static IServiceCollection AddUbl21Format(this IServiceCollection services)
    {
        services.AddSingleton<IFormatReader, Ubl21FormatReader>();
        services.AddSingleton<IFormatWriter, Ubl21FormatWriter>();
        services.AddSingleton<IFormatValidator, Ubl21FormatValidator>();
        return services;
    }
}
