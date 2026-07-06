using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Retry.Simple;
using RabbitMQ.Client;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Messaging.Serialization;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Integration;

/// <summary>
/// Proves the header-bridging approach (<see cref="StampMessageTypeHeaderStep"/>) works
/// against a real broker: a message published exactly the way the existing
/// OpenHub.Accessor.RabbitMq publisher does today (plain JSON, no rbs2-* headers) must
/// still be consumed and correctly deserialized by a Rebus bus.
/// </summary>
[Trait("Category", "Integration")]
[Collection(RabbitMqCollectionDefinition.Name)]
public class RebusConsumerIntegrationTests(RabbitMqCollectionFixture fixture)
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(20);

    private static string UniqueName(string prefix) => $"{prefix}.{Guid.NewGuid():N}";

    private sealed class CapturingHandler : IHandleMessages<ProductChangedEvent>
    {
        public static TaskCompletionSource<ProductChangedEvent> Received = new();

        public Task Handle(ProductChangedEvent message)
        {
            Received.TrySetResult(message);
            return Task.CompletedTask;
        }
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = fixture.HostName,
            Port = fixture.Port,
            UserName = fixture.UserName,
            Password = fixture.Password,
        };
        return await factory.CreateConnectionAsync();
    }

    private async Task DeclareTopologyAsync(string exchange, string queue)
    {
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queue, exchange, routingKey: "");
    }

    // Publishes exactly the way OpenHub.Accessor.RabbitMq's RabbitMqMessagePublisher
    // does today: plain System.Text.Json bytes, content-type header only, NO Rebus
    // headers. This is the point of the test — proving the header-bridging step lets
    // Rebus consume messages written by a non-Rebus producer.
    private async Task PublishHeaderlessAsync(string exchange, byte[] body)
    {
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var props = new BasicProperties { ContentType = "application/json", Persistent = true };
        await channel.BasicPublishAsync(exchange, routingKey: "", mandatory: false, basicProperties: props, body: body);
    }

    private IHost BuildWorkerHost(string queueName, string errorQueueName, int maxDeliveryAttempts = 3)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddRebus(configure => configure
                    .Logging(l => l.MicrosoftExtensionsLogging(NullLoggerFactory.Instance))
                    .Transport(t => t.UseRabbitMq(fixture.ConnectionString, queueName))
                    .Options(o =>
                    {
                        o.RetryStrategy(errorQueueName: errorQueueName, maxDeliveryAttempts: maxDeliveryAttempts);
                        o.Decorate<IPipeline>(c =>
                        {
                            var pipeline = c.Get<IPipeline>();
                            return new PipelineStepInjector(pipeline)
                                .OnReceive(new StampMessageTypeHeaderStep(), PipelineRelativePosition.Before, typeof(DeserializeIncomingMessageStep));
                        });
                    }));

                services.AutoRegisterHandlersFromAssemblyOf<CapturingHandler>();
            })
            .Build();
    }

    [Fact]
    public async Task Consumer_ReceivesHeaderlessMessageFromExistingPublisher_AndDeserializesCorrectly()
    {
        CapturingHandler.Received = new TaskCompletionSource<ProductChangedEvent>();

        var exchange = UniqueName("test.product.changed");
        var queue = UniqueName("test.product-manager-function.product-changed");
        var errorQueue = $"{queue}.error";
        await DeclareTopologyAsync(exchange, queue);

        var evt = new ProductChangedEvent
        {
            ProductId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RealmSlug = "client-a",
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(evt);

        using var host = BuildWorkerHost(queue, errorQueue);
        await host.StartAsync();
        try
        {
            await PublishHeaderlessAsync(exchange, body);

            var received = await CapturingHandler.Received.Task.WaitAsync(ReceiveTimeout);

            received.ProductId.ShouldBe(evt.ProductId);
            received.TenantId.ShouldBe(evt.TenantId);
            received.UserId.ShouldBe(evt.UserId);
            received.RealmSlug.ShouldBe(evt.RealmSlug);
            received.IsDeleted.ShouldBe(evt.IsDeleted);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Consumer_MalformedBody_LandsInErrorQueueAfterMaxDeliveryAttempts()
    {
        var exchange = UniqueName("test.product.changed");
        var queue = UniqueName("test.product-manager-function.product-changed");
        var errorQueue = $"{queue}.error";
        await DeclareTopologyAsync(exchange, queue);

        // Not valid JSON — StampMessageTypeHeaderStep still stamps rbs2-msg-type
        // (since this queue is treated as single-purpose), but the stock JSON
        // deserializer will throw on the malformed body, exhausting retries.
        var malformedBody = "{ not valid json"u8.ToArray();

        using var host = BuildWorkerHost(queue, errorQueue, maxDeliveryAttempts: 2);
        await host.StartAsync();
        try
        {
            await PublishHeaderlessAsync(exchange, malformedBody);

            await using var connection = await CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            var deadline = DateTimeOffset.UtcNow + ReceiveTimeout;
            RabbitMQ.Client.BasicGetResult? errorResult = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                errorResult = await channel.BasicGetAsync(errorQueue, autoAck: true);
                if (errorResult is not null)
                {
                    break;
                }

                await Task.Delay(200);
            }

            errorResult.ShouldNotBeNull("Expected the malformed message to land in the error queue after exhausting retries.");
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
