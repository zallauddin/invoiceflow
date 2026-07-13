using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using InvoiceFlow.Infrastructure.Data;
using InvoiceFlow.Infrastructure.Auth;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Repositories;
using Testcontainers.PostgreSql;

namespace InvoiceFlow.IntegrationTests.Fixtures;

/// <summary>
/// Combined test fixture providing all infrastructure services.
/// Configures a real PostgreSQL database via TestContainers.
/// </summary>
public sealed class TestInfrastructureFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("invoiceflow_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithPortBinding(5432, 0)
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();
    public string Host => _postgres.Hostname;
    public int Port => _postgres.GetMappedPublicPort(5432);

    private IServiceScopeFactory? _scopeFactory;
    private IServiceProvider? _serviceProvider;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();

        // EF Core with PostgreSQL
        services.AddDbContext<InvoiceFlowDbContext>(options =>
        {
            options.UseNpgsql(_postgres.GetConnectionString(), npgsql =>
            {
                npgsql.MigrationsAssembly("InvoiceFlow.Infrastructure");
            });
        });

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantResolver, DatabaseTenantResolver>();

        // Tenant provider (returns null for tests — disables query filters)
        services.AddScoped<ITenantIdProvider>(sp => new NullTenantIdProvider());

        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Apply migrations
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceFlowDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();

        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Creates a new DI scope for test isolation.
    /// </summary>
    public IServiceScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_scopeFactory is null, this);
        return _scopeFactory!.CreateScope();
    }

    /// <summary>
    /// Gets a required service from a new scope.
    /// </summary>
    public T GetRequiredService<T>() where T : notnull
    {
        using var scope = CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}

/// <summary>
/// Null tenant provider for tests — returns null (disables global query filters).
/// </summary>
internal sealed class NullTenantIdProvider : ITenantIdProvider
{
    public Guid? TenantId => null;
}
