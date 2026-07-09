using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using OpenHub.CRM.EventProcessor.Messaging.Setup;

namespace OpenHub.CRM.EventProcessor.Messaging;

/// <summary>
/// Ensures this worker's queue exists, is durable, and is bound to the client.changed
/// fanout exchange — declared idempotently on every startup.
///
/// Registered before AddRebus in <see cref="MessagingServiceExtensions.AddMessaging"/> so
/// the generic host starts it first, guaranteeing the exchange/queue/binding exist before
/// Rebus starts consuming.
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
            messaging.ExchangeName, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            messaging.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(messaging.QueueName, messaging.ExchangeName, routingKey: "", cancellationToken: cancellationToken);

        logger.LogInformation(
            "Ensured queue {Queue} is bound to exchange {Exchange}.", messaging.QueueName, messaging.ExchangeName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
