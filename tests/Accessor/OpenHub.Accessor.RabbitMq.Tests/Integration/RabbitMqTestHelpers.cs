using RabbitMQ.Client;

namespace OpenHub.Accessor.RabbitMq.Tests.Integration;

/// <summary>
/// Builds an independent consumer connection (separate from the publisher
/// under test) so integration tests can black-box-verify what actually landed
/// on the broker, without reaching into the publisher's private state.
/// </summary>
internal static class RabbitMqTestHelpers
{
    public static async Task<(string QueueName, IChannel Channel)> DeclareTemporaryBoundQueueAsync(
        IConnection connection, string exchangeName, CancellationToken ct = default)
    {
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        var declareResult = await channel.QueueDeclareAsync(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: ct);
        await channel.QueueBindAsync(
            queue: declareResult.QueueName,
            exchange: exchangeName,
            routingKey: "",
            cancellationToken: ct);
        return (declareResult.QueueName, channel);
    }

    /// <summary>Polls for a single message with a timeout (fanout queues have no routing key to filter on).</summary>
    public static async Task<BasicGetResult> ConsumeOneAsync(IChannel channel, string queueName, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(queueName, autoAck: true, cancellationToken: CancellationToken.None);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No message arrived on queue '{queueName}' within {timeout}.");
    }
}
