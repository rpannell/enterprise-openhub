using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using OpenHub.ProductManager.IdentityEventProcessor.Messaging.Setup;

namespace OpenHub.ProductManager.IdentityEventProcessor.Messaging;

/// <summary>
/// Ensures both of this worker's queues exist, are durable, and are bound to their
/// respective fanout exchange (tenant.changed / user.changed) — declared idempotently
/// on every startup, same rationale as ElasticProcessor's topology initializer, extended
/// here to declare+bind two exchange/queue pairs instead of one.
///
/// Registered before AddRebus in <see cref="MessagingServiceExtensions.AddMessaging"/>
/// so the generic host starts it first, guaranteeing both exchanges/queues/bindings
/// exist before either Rebus bus starts consuming.
/// </summary>
public class RabbitMqTopologyInitializer(
    IConfiguration configuration,
    IOptions<MessagingOptions> options,
    ILogger<RabbitMqTopologyInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var messaging = options.Value;
        var factory = new ConnectionFactory { Uri = new Uri(MessagingServiceExtensions.GetConnectionString(configuration)) };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await DeclareAndBindAsync(channel, messaging.TenantExchangeName, messaging.TenantQueueName, cancellationToken);
        await DeclareAndBindAsync(channel, messaging.UserExchangeName, messaging.UserQueueName, cancellationToken);

        logger.LogInformation(
            "Ensured queue {TenantQueue} is bound to exchange {TenantExchange}, and queue {UserQueue} is bound to exchange {UserExchange}.",
            messaging.TenantQueueName, messaging.TenantExchangeName, messaging.UserQueueName, messaging.UserExchangeName);
    }

    private static async Task DeclareAndBindAsync(
        IChannel channel, string exchangeName, string queueName, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            exchangeName, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(
            queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(queueName, exchangeName, routingKey: "", cancellationToken: ct);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
