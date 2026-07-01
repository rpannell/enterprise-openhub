# RabbitMqPublisher

A .NET 10 class library for publishing messages to a RabbitMQ **durable fanout exchange**, registered as a **singleton** in an API, built to handle **lots of concurrent messages** using a pool of RabbitMQ **channels** (`IChannel`).

## How it works

- On startup it opens **one shared connection** and declares a **durable fanout exchange** (configurable, defaults to `default.fanout.exchange`).
- It also opens a **pool of RabbitMQ channels** (`ChannelPoolSize`, default 10). AMQP channels aren't thread-safe, so instead of every publish call fighting over a single channel, each call **rents** a channel from the pool, publishes, and returns it.
- `PublishAsync<T>(message)` accepts any object, serializes it to JSON, rents a pooled channel, and calls `BasicPublishAsync`. If all channels are currently busy, the call asynchronously waits for one to free up rather than opening unbounded new channels — this keeps broker-side resource usage predictable under a burst of concurrent publishes.

## Install

Add a project reference. `RabbitMQ.Client` 7.x (the async `IChannel` API) comes along as a dependency.

## Register in your API (`Program.cs`)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Option A: bind from configuration (appsettings.json section "RabbitMq")
builder.Services.AddRabbitMqPublisher(builder.Configuration.GetSection("RabbitMq"));

// Option B: configure in code
// builder.Services.AddRabbitMqPublisher(options =>
// {
//     options.HostName = "localhost";
//     options.ExchangeName = "orders.fanout";
//     options.ChannelPoolSize = 20;
// });

var app = builder.Build();
```

`appsettings.json` example:

```json
{
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "VirtualHost": "/",
    "UserName": "guest",
    "Password": "guest",
    "ExchangeName": "orders.fanout",
    "DurableExchange": true,
    "PersistentMessages": true,
    "ChannelPoolSize": 10
  }
}
```

This registers `RabbitMqMessagePublisher` as:
- a singleton `IMessagePublisher` (what you inject and use), and
- an `IHostedService` (so the connection, exchange, and channel pool are created/torn down with the host automatically).

## Use it from a controller / service

```csharp
public sealed class OrdersController : ControllerBase
{
    private readonly IMessagePublisher _publisher;

    public OrdersController(IMessagePublisher publisher) => _publisher = publisher;

    [HttpPost]
    public async Task<IActionResult> Create(OrderCreatedEvent order, CancellationToken ct)
    {
        // ... persist the order ...
        await _publisher.PublishAsync(order, cancellationToken: ct);
        return Accepted();
    }
}
```

## Tuning for high volume

- Raise `ChannelPoolSize` to allow more publishes to happen truly concurrently.
- `PersistentMessages` controls AMQP delivery mode: `true` marks messages persistent so they survive a broker restart while sitting in a durable queue bound to the exchange; set `false` for maximum throughput if some message loss on a broker crash is acceptable.

## Notes

- The exchange type is fixed to `fanout`, declared `durable: true` by default, matching "durable fan out (default) exchange" — set `DurableExchange = false` only if you intentionally want a non-durable exchange.
- Routing keys are accepted on publish for forward-compatibility but are ignored by RabbitMQ for fanout exchanges (all bound queues receive every message regardless).
- Messages are JSON-serialized via `System.Text.Json`; the body is sent with `content-type: application/json`.
- Targets `net10.0` and uses the RabbitMQ.Client 7.x async API (`CreateChannelAsync`, `BasicPublishAsync`, etc.).
