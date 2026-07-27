namespace OpenHub.Accessor.RabbitMq;

/// <summary>
/// Publishes messages to a RabbitMQ durable fanout exchange.
/// Register and use as a singleton.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Serializes <paramref name="message"/> to JSON and publishes it to
    /// <paramref name="exchangeName"/>, using a pooled RabbitMQ channel so many
    /// concurrent calls can publish at once without contending for a single
    /// channel. The exchange is declared (idempotently, durable fanout) the
    /// first time it's published to, then cached for the lifetime of this
    /// publisher.
    /// </summary>
    /// <typeparam name="T">Type of the message payload.</typeparam>
    /// <param name="message">The object to publish.</param>
    /// <param name="exchangeName">
    /// Name of the durable fanout exchange to publish to. Required — callers
    /// deciding whether an entity's publishing is enabled should do so before
    /// calling this (see <see cref="MessagePublisherExtensions.PublishEntityEventAsync{T}"/>)
    /// rather than passing a null/empty value here.
    /// </param>
    /// <param name="routingKey">
    /// Optional routing key override. Ignored by fanout exchanges but
    /// passed through for forward compatibility.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<T>(T message, string exchangeName, string? routingKey = null, CancellationToken cancellationToken = default);
}
