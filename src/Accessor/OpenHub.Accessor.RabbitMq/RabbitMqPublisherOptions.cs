namespace OpenHub.Accessor.RabbitMq;

/// <summary>
/// Configuration options for the RabbitMQ publisher.
/// Bind this from appsettings.json (e.g. section "RabbitMq").
/// </summary>
public sealed class RabbitMqPublisherOptions
{
    /// <summary>RabbitMQ host name.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>RabbitMQ port. Default AMQP port is 5672.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>Virtual host to connect to.</summary>
    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    /// <summary>
    /// Name of the fanout exchange that will be declared (durable) on startup
    /// and used for all publishes.
    /// </summary>
    public string ExchangeName { get; set; } = "default.fanout.exchange";

    /// <summary>
    /// Routing key used on publish. Fanout exchanges ignore the routing key,
    /// but it is kept here in case the exchange type is ever changed.
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Marks the exchange as durable so it survives a broker restart. Should
    /// stay true to match "durable fan out (default)" requirement.
    /// </summary>
    public bool DurableExchange { get; set; } = true;

    /// <summary>
    /// Whether published messages should be marked persistent (delivery mode 2)
    /// so they survive a broker restart while sitting in a durable queue.
    /// </summary>
    public bool PersistentMessages { get; set; } = true;

    /// <summary>
    /// Number of RabbitMQ channels (IChannel) kept open in the pool for
    /// concurrent publishing. AMQP channels are not thread-safe, so to
    /// publish a lot of messages at once, multiple requests rent different
    /// channels from this pool instead of contending for one. Raise this to
    /// increase publish parallelism.
    /// </summary>
    public int ChannelPoolSize { get; set; } = 10;

    /// <summary>
    /// Automatic recovery for the underlying connection (reconnects and
    /// redeclares topology if the broker connection drops).
    /// </summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);
}
