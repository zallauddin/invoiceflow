using InvoiceFlow.Formats.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Formats.Conversion;

/// <summary>
/// DI extension methods for registering the format conversion pipeline services.
/// </summary>
public static class FormatConversionServiceExtensions
{
    /// <summary>
    /// Registers the core format conversion services: <see cref="FormatRegistry"/> and
    /// <see cref="FormatConversionPipeline"/>.
    /// Format-specific readers, writers, and validators must be registered separately
    /// via their own extension methods (e.g., AddUbl21Format) or via <see cref="FormatRegistrationServiceExtensions.AddAllFormats"/>.
    /// </summary>
    public static IServiceCollection AddFormatConversion(this IServiceCollection services)
    {
        services.AddSingleton<IFormatRegistry, FormatRegistry>();
        services.AddSingleton<IFormatConversionPipeline, FormatConversionPipeline>();
        return services;
    }
}
