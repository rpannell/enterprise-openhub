namespace OpenHub.ProductManager.ElasticProcessor.Messaging;

/// <summary>
/// Configuration options for this worker's RabbitMQ topology.
/// Bind this from appsettings.json / Consul under section "Messaging".
/// </summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>Fanout exchange that <c>ProductChangedEvent</c>s are published to.</summary>
    public string ExchangeName { get; set; } = "product.changed";

    /// <summary>
    /// Durable queue this worker consumes from, bound to <see cref="ExchangeName"/>
    /// on startup. Defaults to the naming artifact carried over from this consumer's
    /// previous identity as OpenHub.ProductManager.Function.
    /// </summary>
    public string QueueName { get; set; } = "product-manager-function.product-changed";

    /// <summary>Where messages land after exhausting Rebus's delivery attempts.</summary>
    public string ErrorQueueName { get; set; } = "product-manager-elastic-processor.error";
}
