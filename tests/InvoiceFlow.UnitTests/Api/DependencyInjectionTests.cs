using FluentAssertions;
using InvoiceFlow.Api;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.UnitTests.Api;

public class DependencyInjectionTests
{
    private static ServiceProvider BuildTestServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
                ["Jwt:Key"] = "TestKey-AtLeast32Characters-Long!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000"
            })
            .Build();

        var services = new ServiceCollection();

        // Add required framework services
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSignalR();

        // Register InvoiceFlow services (the system under test)
        services.AddInvoiceFlowApi(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AddInvoiceFlowApi_ShouldRegisterITenantIdProvider_AsScoped()
    {
        using var provider = BuildTestServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITenantIdProvider>();

        service.Should().NotBeNull();
        service.GetType().Name.Should().Be("HttpContextTenantIdProvider");
    }

    [Fact]
    public void AddInvoiceFlowApi_ShouldRegisterDbContext_AsScoped()
    {
        using var provider = BuildTestServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var ctx1 = scope1.ServiceProvider.GetRequiredService<InvoiceFlowDbContext>();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<InvoiceFlowDbContext>();

        ctx1.Should().NotBeNull();
        ctx2.Should().NotBeNull();
        ctx1.Should().NotBeSameAs(ctx2); // Scoped = different instances per scope
    }

    [Fact]
    public void AddInvoiceFlowApi_ShouldRegisterRepository_AsScoped()
    {
        using var provider = BuildTestServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var repo1 = scope1.ServiceProvider.GetRequiredService<IRepository<InvoiceFlow.Core.Entities.Invoice>>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<IRepository<InvoiceFlow.Core.Entities.Invoice>>();

        repo1.Should().NotBeNull();
        repo2.Should().NotBeNull();
        repo1.Should().NotBeSameAs(repo2);
    }

    [Fact]
    public void AddInvoiceFlowApi_ShouldRegisterMultipleRepositoryTypes()
    {
        using var provider = BuildTestServiceProvider();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        // Verify multiple entity repositories can be resolved
        var invoiceRepo = sp.GetRequiredService<IRepository<InvoiceFlow.Core.Entities.Invoice>>();
        var documentRepo = sp.GetRequiredService<IRepository<InvoiceFlow.Core.Entities.Document>>();
        var userRepo = sp.GetRequiredService<IRepository<InvoiceFlow.Core.Entities.User>>();

        invoiceRepo.Should().NotBeNull();
        documentRepo.Should().NotBeNull();
        userRepo.Should().NotBeNull();
    }

    [Fact]
    public void AddInvoiceFlowApi_ShouldRegisterAuthentication()
    {
        using var provider = BuildTestServiceProvider();

        var authSchemeProvider = provider.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
        authSchemeProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddInvoiceFlowApi_ShouldRegisterAuthorization()
    {
        using var provider = BuildTestServiceProvider();

        var authService = provider.GetService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        authService.Should().NotBeNull();
    }

    [Fact]
    public void AddInvoiceFlowApi_WithNullJwtKey_ShouldThrow()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test"
                // Jwt:Key deliberately missing
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSignalR();

        var act = () => services.AddInvoiceFlowApi(configuration);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Jwt:Key");
    }
}
