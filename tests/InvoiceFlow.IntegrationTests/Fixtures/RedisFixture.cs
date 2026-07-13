using Testcontainers.Redis;

namespace InvoiceFlow.IntegrationTests.Fixtures;

/// <summary>
/// Shared Redis TestContainer for integration tests.
/// Uses a single container across all tests in the assembly (collection fixture).
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .WithPortBinding(6379, 0) // Random host port
        .Build();

    public string Hostname => _container.Hostname;
    public int Port => _container.GetMappedPublicPort(6379);
    public string ConnectionString => $"{_container.Hostname}:{_container.GetMappedPublicPort(6379)}";

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
/// Collection fixture so all tests sharing RedisFixture use the same container.
/// </summary>
[CollectionDefinition("Redis")]
public sealed class RedisCollectionDefinition : ICollectionFixture<RedisFixture>
{
}
