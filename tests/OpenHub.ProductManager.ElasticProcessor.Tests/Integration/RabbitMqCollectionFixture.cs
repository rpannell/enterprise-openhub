using Testcontainers.RabbitMq;
using Xunit;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Integration;

/// <summary>
/// Starts a single real RabbitMQ broker once for the whole integration test
/// class (see <see cref="RabbitMqCollectionDefinition"/>), not once per test.
/// Individual tests still use uniquely-named exchanges/queues to avoid
/// interfering with each other on the shared broker.
/// </summary>
public sealed class RabbitMqCollectionFixture : IAsyncLifetime
{
    private RabbitMqContainer? _container;

    public string HostName => _container!.Hostname;
    public int Port => _container!.GetMappedPublicPort(5672);
    public string UserName => "guest";
    public string Password => "guest";
    public string ConnectionString => $"amqp://{UserName}:{Password}@{HostName}:{Port}/";

    public async Task InitializeAsync()
    {
        _container = new RabbitMqBuilder("rabbitmq:4-management")
            .WithUsername(UserName)
            .WithPassword(Password)
            .Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
