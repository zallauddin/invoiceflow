using Testcontainers.PostgreSql;

namespace InvoiceFlow.IntegrationTests.Fixtures;

/// <summary>
/// Shared PostgreSQL TestContainer for integration tests.
/// Uses a single container across all tests in the assembly (collection fixture).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("invoiceflow_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithPortBinding(5432, 0) // Random host port
        .Build();

    public string ConnectionString => _container.GetConnectionString();
    public string Host => _container.Hostname;
    public int Port => _container.GetMappedPublicPort(5432);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Collection fixture so all tests sharing PostgresFixture use the same container.
/// </summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
}
