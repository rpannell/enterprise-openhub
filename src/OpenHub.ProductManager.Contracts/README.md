# OpenHub.ProductManager.Contracts

Shared wire contracts for messages published by `OpenHub.ProductManager.Api` and consumed by downstream services (currently `OpenHub.ProductManager.Function`). A plain class library with no dependencies — reference it from any project that needs to publish or consume these messages, so the shape can't drift between publisher and consumer.

## Contents

### `ProductChangedEvent`

Published to the `product.changed` durable fanout exchange (see `OpenHub.Accessor.RabbitMq`) whenever a product is created, updated, or soft-deleted.

```csharp
public sealed class ProductChangedEvent
{
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }     // who made the change
    public string RealmSlug { get; set; } // Keycloak realm the change originated from, e.g. "client-a"
    public bool IsDeleted { get; set; }   // true only on delete
}
```

`RealmSlug` is set by `ProductManager.Api`'s `TenantMiddleware`, which already resolves it from the JWT `iss` claim on every authenticated request — this lets consumers request an M2M token for the right tenant realm (see `OpenHub.Accessor.KeyCloak`'s `IKeyCloakM2MTokenAccessor`) without maintaining a separate tenant-to-realm lookup.

Serialized with `System.Text.Json` default options (PascalCase property names, no custom naming policy) on both the publish and consume sides.

## When to add something here

Only wire contracts that cross a process boundary between two or more of these services belong in this project — not internal DTOs (those stay in each service's own `Managers/Models`/`Accessors/Models` per the iDesign layering convention). If a message only ever has one consumer today, it's still worth putting here once that consumer exists, so the shape is defined once instead of duplicated.
