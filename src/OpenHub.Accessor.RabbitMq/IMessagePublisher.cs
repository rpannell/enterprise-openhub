namespace OpenHub.Accessor.RabbitMq;

/// <summary>
/// Publishes messages to the configured RabbitMQ durable fanout exchange.
/// Register and use as a singleton.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Serializes <paramref name="message"/> to JSON and publishes it to the
    /// configured fanout exchange, using a pooled RabbitMQ channel so many
    /// concurrent calls can publish at once without contending for a single
    /// channel.
    /// </summary>
    /// <typeparam name="T">Type of the message payload.</typeparam>
    /// <param name="message">The object to publish.</param>
    /// <param name="routingKey">
    /// Optional routing key override. Ignored by fanout exchanges but
    /// passed through for forward compatibility.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken cancellationToken = default);
}
