namespace OpenHub.CRM.EventProcessor.Messaging;

/// <summary>
/// Configuration for this worker's single RabbitMQ queue, bound to the client.changed
/// fanout exchange. Bind this from appsettings.json / Consul under section "Messaging".
/// </summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public string ExchangeName { get; set; } = "client.changed";
    public string QueueName { get; set; } = "crm-event-processor.client-changed";
    public string ErrorQueueName { get; set; } = "crm-event-processor.client-changed.error";
}
