using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OpenHub.Accessor.RabbitMq;

/// <summary>
/// Singleton RabbitMQ publisher that publishes to a durable fanout exchange
/// using a pool of RabbitMQ <see cref="IChannel"/> instances.
///
/// Design:
///  - One shared, long-lived <see cref="IConnection"/>.
///  - A pool of <see cref="IChannel"/> ("RabbitMQ channels") opened on that
///    connection at startup. AMQP channels are not thread-safe, so to handle
///    a lot of messages at once without callers blocking on each other, each
///    publish call rents a channel from the pool, publishes, and returns it.
///  - Exchanges are declared per name (durable fanout), the first time each
///    name is published to, then cached — since exchange names are now
///    supplied per publish call (one per entity, see
///    <see cref="EntityEventOptions"/>) rather than fixed once at startup.
///
/// Register via <see cref="ServiceCollectionExtensions.AddRabbitMqPublisher"/>,
/// which wires this up as a singleton <see cref="IMessagePublisher"/> and as
/// an <see cref="IHostedService"/> so the connection/pool/exchange are set up
/// and torn down with the host automatically.
/// </summary>
public sealed class RabbitMqMessagePublisher : IMessagePublisher, IHostedService, IAsyncDisposable
{
    private readonly RabbitMqPublisherOptions _options;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;

    private IConnection? _connection;
    private ConcurrentQueue<IChannel>? _channelPool;
    private SemaphoreSlim? _poolAvailability;
    private ConcurrentDictionary<string, byte> _declaredExchanges = new();
    private bool _disposed;

    public RabbitMqMessagePublisher(IOptions<RabbitMqPublisherOptions> options, ILogger<RabbitMqMessagePublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// Opens the connection, declares the durable fanout exchange, and opens
    /// the pool of RabbitMQ channels used for publishing.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName,
            Password = _options.Password,
            AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
            NetworkRecoveryInterval = _options.NetworkRecoveryInterval,
        };

        _connection = await factory.CreateConnectionAsync(
            $"{nameof(RabbitMqMessagePublisher)}-connection", cancellationToken);

        var poolSize = Math.Max(1, _options.ChannelPoolSize);
        var pool = new ConcurrentQueue<IChannel>();

        for (var i = 0; i < poolSize; i++)
        {
            var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            pool.Enqueue(channel);
        }

        _channelPool = pool;
        _poolAvailability = new SemaphoreSlim(poolSize, poolSize);
        _declaredExchanges = new ConcurrentDictionary<string, byte>();

        _logger.LogInformation(
            "RabbitMqMessagePublisher connected to {HostName}:{Port} with a pool of {PoolSize} channel(s).",
            _options.HostName, _options.Port, poolSize);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channelPool is not null)
        {
            while (_channelPool.TryDequeue(out var channel))
            {
                try
                {
                    await channel.CloseAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing a pooled RabbitMQ channel during shutdown.");
                }
                finally
                {
                    await channel.DisposeAsync();
                }
            }
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
        }

        // Clear so a post-stop PublishAsync hits the clean "not started" guard
        // below instead of failing deeper with a low-level RabbitMQ.Client
        // exception from a closed channel/connection.
        _channelPool = null;
        _poolAvailability = null;
        _declaredExchanges = new ConcurrentDictionary<string, byte>();
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(T message, string exchangeName, string? routingKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);

        if (_channelPool is null || _poolAvailability is null)
        {
            throw new InvalidOperationException(
                $"{nameof(RabbitMqMessagePublisher)} has not been started. Ensure it is registered as a hosted service " +
                "(via AddRabbitMqPublisher) so StartAsync runs before publishing.");
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        // Rent a channel from the pool. If every channel is currently busy
        // publishing, this asynchronously waits instead of creating unbounded
        // new channels, which keeps broker-side resource usage predictable
        // even under a heavy burst of concurrent publish calls.
        await _poolAvailability.WaitAsync(cancellationToken);

        if (!_channelPool.TryDequeue(out var channel))
        {
            _poolAvailability.Release();
            throw new InvalidOperationException("Channel pool availability and queue are out of sync.");
        }

        try
        {
            await EnsureExchangeDeclaredAsync(channel, exchangeName, cancellationToken);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                Persistent = _options.PersistentMessages,
            };

            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey ?? string.Empty,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to exchange '{Exchange}'.", exchangeName);
            throw;
        }
        finally
        {
            _channelPool.Enqueue(channel);
            _poolAvailability.Release();
        }
    }

    /// <summary>
    /// Declares <paramref name="exchangeName"/> as a durable fanout exchange the first
    /// time it's seen, then remembers it so every later publish to the same exchange
    /// skips the redeclare. RabbitMQ no-ops an identical redeclare (same type/durability)
    /// anyway, so a rare race between two concurrent first-publishes to a brand-new
    /// exchange name is harmless.
    /// </summary>
    private async Task EnsureExchangeDeclaredAsync(IChannel channel, string exchangeName, CancellationToken cancellationToken)
    {
        if (_declaredExchanges.ContainsKey(exchangeName))
        {
            return;
        }

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Fanout,
            durable: _options.DurableExchange,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        _declaredExchanges[exchangeName] = 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var poolAvailability = _poolAvailability;
        await StopAsync(CancellationToken.None);
        poolAvailability?.Dispose();

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
