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
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Messaging.Serialization;

namespace OpenHub.ProductManager.IdentityEventProcessor.Tests.Integration;

/// <summary>
/// Proves this worker's two-queue shape end to end against a real broker: a message
/// published headerlessly (the same way the Keycloak listener does) to the tenant.changed
/// exchange is only ever consumed off the tenant queue, and likewise for user.changed —
/// each bus only ever sees the message type its own queue was declared for.
/// </summary>
[Trait("Category", "Integration")]
[Collection(RabbitMqCollectionDefinition.Name)]
public class RebusMultiQueueConsumerIntegrationTests(RabbitMqCollectionFixture fixture)
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(20);

    private static string UniqueName(string prefix) => $"{prefix}.{Guid.NewGuid():N}";

    // Concrete (non-generic) handler classes — AutoRegisterHandlersFromAssemblyOf can't
    // resolve an open-generic IHandleMessages<T> implementation via DI, so this can't be a
    // single CapturingHandler<TMessage> class.
    private sealed class TenantCapturingHandler : IHandleMessages<TenantChangedEvent>
    {
        public static TaskCompletionSource<TenantChangedEvent> Received = new();

        public Task Handle(TenantChangedEvent message)
        {
            Received.TrySetResult(message);
            return Task.CompletedTask;
        }
    }

    private sealed class UserCapturingHandler : IHandleMessages<UserChangedEvent>
    {
        public static TaskCompletionSource<UserChangedEvent> Received = new();

        public Task Handle(UserChangedEvent message)
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

    private async Task DeclareAndBindAsync(string exchange, string queue)
    {
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queue, exchange, routingKey: "");
    }

    private async Task PublishHeaderlessAsync(string exchange, byte[] body)
    {
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var props = new BasicProperties { ContentType = "application/json", Persistent = true };
        await channel.BasicPublishAsync(exchange, routingKey: "", mandatory: false, basicProperties: props, body: body);
    }

    private IHost BuildTenantBusHost(string queueName, string errorQueueName)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddRebus(configure => configure
                    .Logging(l => l.MicrosoftExtensionsLogging(NullLoggerFactory.Instance))
                    .Transport(t => t.UseRabbitMq(fixture.ConnectionString, queueName))
                    .Options(o =>
                    {
                        o.RetryStrategy(errorQueueName: errorQueueName, maxDeliveryAttempts: 3);
                        o.Decorate<IPipeline>(c =>
                        {
                            var pipeline = c.Get<IPipeline>();
                            return new PipelineStepInjector(pipeline)
                                .OnReceive(new StampMessageTypeHeaderStep<TenantChangedEvent>(), PipelineRelativePosition.Before, typeof(DeserializeIncomingMessageStep));
                        });
                    }));

                services.AddRebusHandler<TenantCapturingHandler>();
            })
            .Build();
    }

    private IHost BuildUserBusHost(string queueName, string errorQueueName)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddRebus(configure => configure
                    .Logging(l => l.MicrosoftExtensionsLogging(NullLoggerFactory.Instance))
                    .Transport(t => t.UseRabbitMq(fixture.ConnectionString, queueName))
                    .Options(o =>
                    {
                        o.RetryStrategy(errorQueueName: errorQueueName, maxDeliveryAttempts: 3);
                        o.Decorate<IPipeline>(c =>
                        {
                            var pipeline = c.Get<IPipeline>();
                            return new PipelineStepInjector(pipeline)
                                .OnReceive(new StampMessageTypeHeaderStep<UserChangedEvent>(), PipelineRelativePosition.Before, typeof(DeserializeIncomingMessageStep));
                        });
                    }));

                services.AddRebusHandler<UserCapturingHandler>();
            })
            .Build();
    }

    [Fact]
    public async Task TenantQueue_ReceivesOnlyTenantChangedMessages()
    {
        TenantCapturingHandler.Received = new TaskCompletionSource<TenantChangedEvent>();

        var exchange = UniqueName("test.tenant.changed");
        var queue = UniqueName("test.identity-event-processor.tenant-changed");
        await DeclareAndBindAsync(exchange, queue);

        var evt = new TenantChangedEvent
        {
            TenantId = Guid.NewGuid(),
            RealmSlug = "client-a",
            Operation = IdentityChangeOperation.Updated,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Name = "Acme Corp",
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(evt);

        using var host = BuildTenantBusHost(queue, $"{queue}.error");
        await host.StartAsync();
        try
        {
            await PublishHeaderlessAsync(exchange, body);

            var received = await TenantCapturingHandler.Received.Task.WaitAsync(ReceiveTimeout);

            received.TenantId.ShouldBe(evt.TenantId);
            received.RealmSlug.ShouldBe(evt.RealmSlug);
            received.Operation.ShouldBe(evt.Operation);
            received.Name.ShouldBe(evt.Name);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task UserQueue_ReceivesOnlyUserChangedMessages()
    {
        UserCapturingHandler.Received = new TaskCompletionSource<UserChangedEvent>();

        var exchange = UniqueName("test.user.changed");
        var queue = UniqueName("test.identity-event-processor.user-changed");
        await DeclareAndBindAsync(exchange, queue);

        var evt = new UserChangedEvent
        {
            UserId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            RealmSlug = "client-a",
            Operation = IdentityChangeOperation.Created,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Email = "a@example.com",
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(evt);

        using var host = BuildUserBusHost(queue, $"{queue}.error");
        await host.StartAsync();
        try
        {
            await PublishHeaderlessAsync(exchange, body);

            var received = await UserCapturingHandler.Received.Task.WaitAsync(ReceiveTimeout);

            received.UserId.ShouldBe(evt.UserId);
            received.TenantId.ShouldBe(evt.TenantId);
            received.Email.ShouldBe(evt.Email);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
