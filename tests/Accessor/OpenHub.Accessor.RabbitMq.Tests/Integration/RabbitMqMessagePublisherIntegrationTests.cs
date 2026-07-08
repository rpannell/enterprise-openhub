using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shouldly;
using Xunit;

namespace OpenHub.Accessor.RabbitMq.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(RabbitMqCollectionDefinition.Name)]
public class RabbitMqMessagePublisherIntegrationTests(RabbitMqCollectionFixture fixture)
{
    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(10);

    private RabbitMqPublisherOptions CreateOptions(string exchangeName, Action<RabbitMqPublisherOptions>? configure = null)
    {
        var options = new RabbitMqPublisherOptions
        {
            HostName = fixture.HostName,
            Port = fixture.Port,
            UserName = fixture.UserName,
            Password = fixture.Password,
            ExchangeName = exchangeName,
            ChannelPoolSize = 3,
        };
        configure?.Invoke(options);
        return options;
    }

    private static async Task<RabbitMqMessagePublisher> CreateStartedPublisherAsync(RabbitMqPublisherOptions options)
    {
        var publisher = new RabbitMqMessagePublisher(Options.Create(options), NullLogger<RabbitMqMessagePublisher>.Instance);
        await publisher.StartAsync(CancellationToken.None);
        return publisher;
    }

    private async Task<IConnection> CreateConsumerConnectionAsync()
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

    // Every test uses its own uniquely-named exchange so tests can run against
    // the one shared broker without interfering with each other.
    private static string UniqueExchange() => $"test.exchange.{Guid.NewGuid():N}";

    [Fact]
    public async Task StartAsync_ConnectsAndDeclaresDurableFanoutExchange()
    {
        var exchange = UniqueExchange();
        await using var publisher = await CreateStartedPublisherAsync(CreateOptions(exchange));

        // RabbitMQ no-ops on an identical re-declare (same type/durability) and
        // throws on a mismatch — this indirectly proves the publisher declared
        // the exchange as durable fanout.
        await using var consumerConnection = await CreateConsumerConnectionAsync();
        await using var verifyChannel = await consumerConnection.CreateChannelAsync();
        await verifyChannel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task PublishAsync_ValidMessage_MessageArrivesOnBoundQueueWithExpectedBody()
    {
        var exchange = UniqueExchange();
        await using var publisher = await CreateStartedPublisherAsync(CreateOptions(exchange));
        await using var consumerConnection = await CreateConsumerConnectionAsync();
        var (queueName, consumerChannel) = await RabbitMqTestHelpers.DeclareTemporaryBoundQueueAsync(consumerConnection, exchange);
        await using var _ = consumerChannel;

        await publisher.PublishAsync(new { Foo = "bar", Count = 42 });

        var received = await RabbitMqTestHelpers.ConsumeOneAsync(consumerChannel, queueName, ConsumeTimeout);
        using var doc = JsonDocument.Parse(received.Body.ToArray());
        // JsonSerializer.SerializeToUtf8Bytes uses default options in production
        // code (no camelCase naming policy), so property names stay PascalCase.
        doc.RootElement.GetProperty("Foo").GetString().ShouldBe("bar");
        doc.RootElement.GetProperty("Count").GetInt32().ShouldBe(42);
    }

    [Fact]
    public async Task PublishAsync_Message_SetsContentTypeApplicationJsonAndPersistentDeliveryMode()
    {
        var exchange = UniqueExchange();
        await using var publisher = await CreateStartedPublisherAsync(CreateOptions(exchange));
        await using var consumerConnection = await CreateConsumerConnectionAsync();
        var (queueName, consumerChannel) = await RabbitMqTestHelpers.DeclareTemporaryBoundQueueAsync(consumerConnection, exchange);
        await using var _ = consumerChannel;

        await publisher.PublishAsync(new { Value = 1 });

        var received = await RabbitMqTestHelpers.ConsumeOneAsync(consumerChannel, queueName, ConsumeTimeout);
        received.BasicProperties.ContentType.ShouldBe("application/json");
        received.BasicProperties.Persistent.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishAsync_Message_WithPersistentMessagesFalse_SetsNonPersistentDeliveryMode()
    {
        var exchange = UniqueExchange();
        await using var publisher = await CreateStartedPublisherAsync(CreateOptions(exchange, o => o.PersistentMessages = false));
        await using var consumerConnection = await CreateConsumerConnectionAsync();
        var (queueName, consumerChannel) = await RabbitMqTestHelpers.DeclareTemporaryBoundQueueAsync(consumerConnection, exchange);
        await using var _ = consumerChannel;

        await publisher.PublishAsync(new { Value = 1 });

        var received = await RabbitMqTestHelpers.ConsumeOneAsync(consumerChannel, queueName, ConsumeTimeout);
        received.BasicProperties.Persistent.ShouldBeFalse();
    }

    [Fact]
    public async Task PublishAsync_CustomRoutingKeyPassed_MessageStillArrivesViaFanout()
    {
        var exchange = UniqueExchange();
        await using var publisher = await CreateStartedPublisherAsync(CreateOptions(exchange));
        await using var consumerConnection = await CreateConsumerConnectionAsync();
        var (queueName, consumerChannel) = await RabbitMqTestHelpers.DeclareTemporaryBoundQueueAsync(consumerConnection, exchange);
        await using var _ = consumerChannel;

        // Fanout exchanges ignore routing keys entirely — the message should
        // still arrive on every bound queue regardless of what's passed here.
        await publisher.PublishAsync(new { Value = 1 }, routingKey: "some.ignored.routing.key");

        await RabbitMqTestHelpers.ConsumeOneAsync(consumerChannel, queueName, ConsumeTimeout);
    }

    [Fact]
    public async Task PublishAsync_ConcurrentPublishesExceedingPoolSize_AllSucceedByWaitingForFreeChannel()
    {
        var exchange = UniqueExchange();
        await using var publisher = await CreateStartedPublisherAsync(CreateOptions(exchange, o => o.ChannelPoolSize = 2));
        await using var consumerConnection = await CreateConsumerConnectionAsync();
        var (queueName, consumerChannel) = await RabbitMqTestHelpers.DeclareTemporaryBoundQueueAsync(consumerConnection, exchange);
        await using var _ = consumerChannel;

        const int messageCount = 10;
        var publishTasks = Enumerable.Range(0, messageCount)
            .Select(i => publisher.PublishAsync(new { Index = i }))
            .ToArray();
        await Task.WhenAll(publishTasks);

        var receivedCount = 0;
        for (var i = 0; i < messageCount; i++)
        {
            await RabbitMqTestHelpers.ConsumeOneAsync(consumerChannel, queueName, ConsumeTimeout);
            receivedCount++;
        }

        receivedCount.ShouldBe(messageCount);
    }

    [Fact]
    public async Task PublishAsync_ConcurrentPublishesExceedingPoolSize_CompletesWithinReasonableTimeout()
    {
        var exchange = UniqueExchange();
        await using var publisher = await CreateStartedPublisherAsync(CreateOptions(exchange, o => o.ChannelPoolSize = 2));

        var burst = Task.WhenAll(Enumerable.Range(0, 20).Select(i => publisher.PublishAsync(new { Index = i })));
        var completed = await Task.WhenAny(burst, Task.Delay(TimeSpan.FromSeconds(10)));

        completed.ShouldBe(burst);
    }

    [Fact]
    public async Task StopAsync_ClosesChannelsAndConnection_SubsequentPublishThrowsInvalidOperationException()
    {
        var exchange = UniqueExchange();
        var publisher = await CreateStartedPublisherAsync(CreateOptions(exchange));

        await publisher.StopAsync(CancellationToken.None);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => publisher.PublishAsync(new { Value = 1 }));
        ex.Message.ShouldContain("has not been started");
    }

    [Fact]
    public async Task StopAsync_CalledWithoutStartAsync_CompletesWithoutThrowing()
    {
        var publisher = new RabbitMqMessagePublisher(
            Options.Create(CreateOptions(UniqueExchange())),
            NullLogger<RabbitMqMessagePublisher>.Instance);

        await publisher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DisposeAsync_AfterStart_ClosesConnectionAndIsIdempotent()
    {
        var exchange = UniqueExchange();
        var publisher = await CreateStartedPublisherAsync(CreateOptions(exchange));

        await publisher.DisposeAsync();
        await publisher.DisposeAsync();
    }
}
