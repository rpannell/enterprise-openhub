using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using OpenHub.ProductManager.ElasticProcessor.Messaging.Setup;

namespace OpenHub.ProductManager.ElasticProcessor.Messaging;

/// <summary>
/// Ensures this worker's queue exists, is durable, and is bound to the
/// product-changed fanout exchange — declared idempotently on every startup, so
/// this worker no longer depends solely on the dev-only Aspire mechanism
/// (RabbitMq/definitions.json, bind-mounted into the broker container) to have
/// correct topology. That matters for any environment that doesn't provision the
/// broker that way — e.g. a future Kubernetes deployment.
///
/// Registered before AddRebus in <see cref="MessagingServiceExtensions.AddMessaging"/>
/// so the generic host starts it first, guaranteeing the exchange/queue/binding
/// exist before Rebus's own hosted service starts declaring/consuming the queue.
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

        await channel.ExchangeDeclareAsync(
            messaging.ExchangeName, ExchangeType.Fanout, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            messaging.QueueName, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            messaging.QueueName, messaging.ExchangeName, routingKey: "", cancellationToken: cancellationToken);

        logger.LogInformation(
            "Ensured queue {QueueName} is bound to exchange {ExchangeName}.",
            messaging.QueueName, messaging.ExchangeName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
