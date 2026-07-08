# OpenHub.Platform.IdentityEvents.Contracts

Wire contracts for the platform-wide **tenant/user change fan-out**: Keycloak publishes
`TenantChangedEvent`/`UserChangedEvent` messages whenever an organization, user, or
organization membership changes, and any domain that cares binds its own queue to pick
them up. A plain class library with **zero dependencies** — reference it from any
project that needs to publish or consume these messages, so the shape can't drift.

This is a different (and simpler) shape than `OpenHub.ProductManager.Contracts`: those
contracts are owned by one domain (ProductManager) and consumed by that domain's own
workers. These contracts are owned by the platform (Keycloak is the publisher, not any
one domain) and are meant to be consumed by **many** domains over time.

## The pattern, end to end

```
Keycloak (admin console / admin API)
  └─ RabbitMqEventListenerProvider (Java SPI, keycloak-rabbitmq-listener/)
     └─ declares + publishes to two durable fanout exchanges:
          "tenant.changed"  (ResourceType.ORGANIZATION admin events)
          "user.changed"    (ResourceType.USER / ORGANIZATION_MEMBERSHIP admin events)
        — never declares or binds a queue on either exchange itself.

Each interested domain:
  └─ creates its own durable queue(s), bound to the exchange(s) it cares about
  └─ consumes with its own worker
  └─ acts on its own data — typically by calling back into that domain's own API
     (not by reaching into another domain's database directly)
```

**Reference example:** `OpenHub.ProductManager.IdentityEventProcessor` is the first
consumer of this fan-out. It's a Worker Service living inside the ProductManager
domain (next to `OpenHub.ProductManager.ElasticProcessor` and
`OpenHub.ProductManager.ReindexJob`) that:
- declares and binds two queues, one per exchange, each consumed by its own Rebus bus
  in the same process
- on `TenantChangedEvent`/`UserChangedEvent`, calls new admin-gated sync endpoints on
  `OpenHub.ProductManager.Api` (`POST /api/v1/admin/tenants/sync`,
  `POST /api/v1/admin/users/sync`) to upsert or soft-delete the corresponding
  `catalog.tenants`/`catalog.users` row and refresh the `HybridCache` entries
  `ProductManager.Api` already maintains for them

**For a future domain** (e.g. Helpdesk) that also needs to react to tenant/user
changes: add a project reference to this contracts library, build a small
`OpenHub.<Domain>.IdentityEventProcessor` worker following the same shape — its own
queue(s) bound to `tenant.changed`/`user.changed`, its own Rebus bus(es), and its own
admin sync endpoints on that domain's own API. Don't share a worker or a queue across
domains; each domain owns its own copy of the consumer side, the same way each domain
owns its own database.

## Contents

### `TenantChangedEvent`

Published to the `tenant.changed` durable fanout exchange whenever a Keycloak
organization is created, updated, or deleted.

```csharp
public sealed class TenantChangedEvent
{
    public string EventType { get; set; } = "TenantChanged";
    public Guid TenantId { get; set; }
    public string RealmSlug { get; set; } = null!;
    public IdentityChangeOperation Operation { get; set; } // Created | Updated | Deleted
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? Name { get; set; }
}
```

### `UserChangedEvent`

Published to the `user.changed` durable fanout exchange whenever a Keycloak user is
created, updated, or deleted, or an organization membership changes.

```csharp
public sealed class UserChangedEvent
{
    public string EventType { get; set; } = "UserChanged";
    public string UserId { get; set; } = null!;
    public Guid? TenantId { get; set; } // only set for organization-membership events
    public string RealmSlug { get; set; } = null!;
    public IdentityChangeOperation Operation { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
```

`RealmSlug` on both mirrors `ProductChangedEvent.RealmSlug` — it lets consumers request
an M2M token for the right tenant realm without maintaining a separate
tenant-to-realm lookup.

`IdentityChangeOperation` is serialized as a string (`Created`/`Updated`/`Deleted`) via
`JsonStringEnumConverter` on both sides of the wire — the Java listener writes the enum
name directly as a string, and this project's `[JsonConverter]` attribute reads it back
the same way.

Serialized with `System.Text.Json` default options (PascalCase property names, no
custom naming policy) on the consume side; the Java publisher writes matching
PascalCase field names by convention (see `TenantChangedEventDto`/`UserChangedEventDto`
in `keycloak-rabbitmq-listener`).

## When to add something here

Only wire contracts published by the platform-wide Keycloak fan-out belong in this
project. A domain-specific event that only one domain publishes and consumes (like
`ProductChangedEvent`) belongs in that domain's own `*.Contracts` project instead.
