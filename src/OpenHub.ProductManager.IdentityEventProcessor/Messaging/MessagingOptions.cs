namespace OpenHub.ProductManager.IdentityEventProcessor.Messaging;

/// <summary>
/// Configuration for this worker's two independent RabbitMQ queues — one bound to
/// tenant.changed, one bound to user.changed, each consumed by its own Rebus bus in
/// this process (see <see cref="Setup.MessagingServiceExtensions"/>). Bind this from
/// appsettings.json / Consul under section "Messaging".
/// </summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public string TenantExchangeName { get; set; } = "tenant.changed";
    public string TenantQueueName { get; set; } = "product-manager-identity-event-processor.tenant-changed";
    public string TenantErrorQueueName { get; set; } = "product-manager-identity-event-processor.tenant-changed.error";

    public string UserExchangeName { get; set; } = "user.changed";
    public string UserQueueName { get; set; } = "product-manager-identity-event-processor.user-changed";
    public string UserErrorQueueName { get; set; } = "product-manager-identity-event-processor.user-changed.error";
}
