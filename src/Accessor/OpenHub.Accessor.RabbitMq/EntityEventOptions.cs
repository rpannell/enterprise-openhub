namespace OpenHub.Accessor.RabbitMq;

/// <summary>
/// Configuration for publishing one entity type's create/update/delete change
/// events. Every entity that publishes events gets its own sealed subclass of
/// this (e.g. <c>ProductEventOptions : EntityEventOptions;</c>) bound to its own
/// configuration section — same one-property shape every time, so adding a new
/// entity is just "copy the pattern."
/// </summary>
public abstract class EntityEventOptions
{
    /// <summary>
    /// Name of the durable fanout exchange to publish this entity's change
    /// events to. Leave null or empty to disable publishing for this entity —
    /// see <see cref="MessagePublisherExtensions.PublishEntityEventAsync{T}"/>.
    /// </summary>
    public string? ExchangeName { get; set; }
}
