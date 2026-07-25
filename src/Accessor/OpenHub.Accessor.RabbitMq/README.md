# RabbitMqPublisher

A .NET 10 class library for publishing messages to a RabbitMQ **durable fanout exchange**, registered as a **singleton** in an API, built to handle **lots of concurrent messages** using a pool of RabbitMQ **channels** (`IChannel`).

## How it works

- On startup it opens **one shared connection** and a **pool of RabbitMQ channels** (`ChannelPoolSize`, default 10). AMQP channels aren't thread-safe, so instead of every publish call fighting over a single channel, each call **rents** a channel from the pool, publishes, and returns it.
- `PublishAsync<T>(message, exchangeName)` accepts any object and an exchange name, serializes the object to JSON, rents a pooled channel, declares the exchange (durable fanout) the first time that name is seen — then calls `BasicPublishAsync`. Declared exchange names are cached for the life of the publisher, so repeat publishes to the same exchange skip the redeclare. If all channels are currently busy, the call asynchronously waits for one to free up rather than opening unbounded new channels — this keeps broker-side resource usage predictable under a burst of concurrent publishes.
- The exchange is **not** fixed at startup — it's supplied per publish call, which is what lets each entity in a process publish to (or be configured off from) its own exchange. See "Per-entity configuration" below.

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
    "DurableExchange": true,
    "PersistentMessages": true,
    "ChannelPoolSize": 10
  }
}
```

This registers `RabbitMqMessagePublisher` as:
- a singleton `IMessagePublisher` (what you inject and use), and
- an `IHostedService` (so the connection and channel pool are created/torn down with the host automatically).

## Per-entity configuration

Every entity that publishes create/update/delete events gets its own exchange name, configurable
independently, using the same pattern:

1. Add a one-line sealed subclass of `EntityEventOptions` for the entity:
   ```csharp
   public sealed class OrderEventOptions : EntityEventOptions;
   ```
2. Bind it to its own config section in `Program.cs`, next to `AddRabbitMqPublisher`:
   ```csharp
   builder.Services.AddRabbitMqPublisher(builder.Configuration.GetSection("RabbitMq"));
   builder.Services.Configure<OrderEventOptions>(builder.Configuration.GetSection("Events:Order"));
   ```
   ```json
   {
     "Events": {
       "Order": { "ExchangeName": "order.changed" }
     }
   }
   ```
3. Inject `IMessagePublisher` and `IOptions<OrderEventOptions>` into the manager, and publish via
   `PublishEntityEventAsync` after every successful create/update/delete:
   ```csharp
   public sealed class OrderManager(IMessagePublisher publisher, IOptions<OrderEventOptions> orderEvents)
   {
       public async Task<Order> CreateAsync(CreateOrderRequest request, CancellationToken ct)
       {
           var order = await _accessor.CreateAsync(request, ct);
           await publisher.PublishEntityEventAsync(orderEvents.Value, new OrderChangedEvent { OrderId = order.Id }, ct);
           return order;
       }
   }
   ```

`PublishEntityEventAsync` is a no-op when `ExchangeName` is null, empty, or whitespace — that's how
publishing for a given entity is turned off, without any conditional logic in the manager itself.
Leaving `ExchangeName` unset is the default for a new entity (opt-in, not opt-out).

## Use it directly (bypassing the per-entity helper)

```csharp
public sealed class OrdersController : ControllerBase
{
    private readonly IMessagePublisher _publisher;

    public OrdersController(IMessagePublisher publisher) => _publisher = publisher;

    [HttpPost]
    public async Task<IActionResult> Create(OrderCreatedEvent order, CancellationToken ct)
    {
        // ... persist the order ...
        await _publisher.PublishAsync(order, "orders.fanout", cancellationToken: ct);
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
