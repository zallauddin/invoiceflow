using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Compliance.Brazil;
using InvoiceFlow.Infrastructure.Compliance.France;
using InvoiceFlow.Infrastructure.Compliance.India;
using InvoiceFlow.Infrastructure.Compliance.Italy;
using InvoiceFlow.Infrastructure.Compliance.Mexico;
using InvoiceFlow.Infrastructure.Compliance.Peppol;
using InvoiceFlow.Infrastructure.Compliance.Poland;
using InvoiceFlow.Infrastructure.Compliance.Zatca;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Compliance;

/// <summary>
/// DI extension methods for registering the <see cref="ComplianceOrchestrator"/>
/// and all underlying country-specific compliance services.
/// </summary>
public static class ComplianceOrchestratorExtensions
{
    /// <summary>
    /// Registers the <see cref="IComplianceOrchestrator"/> and all country-specific compliance services
    /// (Peppol, Zatca, BrazilNfe, IndiaIrp, MexicoCfdi, ItalySdi, FrancePpf, PolandKsef).
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddComplianceOrchestrator(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register all country-specific compliance services
        services.AddPeppolCompliance(configuration);
        services.AddZatcaCompliance(configuration);
        services.AddBrazilNfeCompliance(configuration);
        services.AddIndiaIrpCompliance(configuration);
        services.AddMexicoCfdiCompliance(configuration);
        services.AddItalySdiCompliance(configuration);
        services.AddFrancePpfCompliance(configuration);
        services.AddPolandKsefCompliance(configuration);

        // Register the orchestrator
        services.AddTransient<IComplianceOrchestrator, ComplianceOrchestrator>();

        return services;
    }
}
