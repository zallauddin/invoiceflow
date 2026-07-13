using Testcontainers.RabbitMq;

namespace InvoiceFlow.IntegrationTests.Fixtures;

/// <summary>
/// Shared RabbitMQ TestContainer for integration tests.
/// Uses a single container across all tests in the assembly (collection fixture).
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .WithPortBinding(5672, 0) // Random host port
        .WithPortBinding(15672, 0) // Management UI
        .Build();

    public string Hostname => _container.Hostname;
    public int Port => _container.GetMappedPublicPort(5672);
    public int ManagementPort => _container.GetMappedPublicPort(15672);

    public string ConnectionString => $"amqp://guest:guest@{_container.Hostname}:{_container.GetMappedPublicPort(5672)}/";

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
/// Collection fixture so all tests sharing RabbitMqFixture use the same container.
/// </summary>
[CollectionDefinition("RabbitMq")]
public sealed class RabbitMqCollectionDefinition : ICollectionFixture<RabbitMqFixture>
{
}
