namespace OpenHub.Accessor.RabbitMq;

public static class MessagePublisherExtensions
{
    /// <summary>
    /// Publishes <paramref name="message"/> to the exchange configured in
    /// <paramref name="options"/>. This is the one place the "null/empty
    /// exchange name disables publishing" rule lives — callers (typically a
    /// manager's Create/Update/Delete) just call this unconditionally instead
    /// of checking the configuration themselves.
    /// </summary>
    public static Task PublishEntityEventAsync<T>(
        this IMessagePublisher publisher,
        EntityEventOptions options,
        T message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.ExchangeName)
            ? Task.CompletedTask
            : publisher.PublishAsync(message, options.ExchangeName, cancellationToken: cancellationToken);
    }
}
