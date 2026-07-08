# OpenHub Product Manager Elastic Processor

Plain .NET Worker Service (Rebus over RabbitMQ, no Azure Functions host) that reacts to product changes published by `OpenHub.ProductManager.Api`. First consumer of the `product.changed` RabbitMQ exchange, and the successor to the RabbitMQ-triggered function that used to live in `OpenHub.ProductManager.Function` before that project was retired down to just its timer trigger.

## What it does

| Queue | Handler | Behavior |
|---|---|---|
| `product-manager-function.product-changed` | `ProductChangedEventHandler` | Deserializes a `ProductChangedEvent`, gets an M2M token for the event's tenant realm, fetches the product from `ProductManager.Api`, and indexes it into the shared `products` Elasticsearch index under a per-tenant filtered alias (`products-tenant-{tenantId}`). Deleted products are removed from the index instead of fetched. |

The default queue name is a naming artifact carried over from this consumer's previous life as `OpenHub.ProductManager.Function`, kept as the default so `src/Platform/OpenHub.Platform.AppHost/RabbitMq/definitions.json` (which also declares the `product.changed` fanout exchange, this queue, and the binding between them, for the dev broker) didn't need to change. Both the exchange and queue name are configurable — see "Messaging (Rebus + RabbitMQ)" below — and the worker also declares and binds them itself on every startup, so it isn't solely dependent on that dev-only broker import.

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | `dotnet --version` |
| .NET Aspire workload | latest | `dotnet workload install aspire` |
| Docker Desktop | any | Required for all containerized dependencies, and for the deployed container image |

---

## Running with Aspire (recommended)

```powershell
cd src
dotnet run --project Platform/OpenHub.Platform.AppHost
```

Aspire starts RabbitMQ, Keycloak, Consul, Vault, Redis, `ProductManager.Api`, and this worker together, with service discovery and the RabbitMQ connection string wired up automatically (`.WithReference(rabbitmq)` injects `ConnectionStrings:RabbitMq`; `.WithReference(productManagerApi)` resolves the API's URL — no hardcoded endpoints to keep in sync).

This worker has no HTTP triggers, so there's no browsable endpoint — watch its output via the Aspire dashboard (http://localhost:18888) instead.

To see it fire: create, update, or delete a product via `ProductManager.Api`, then check this worker's logs in the dashboard.

---

## Running standalone (without Aspire)

**Required environment variables (or `appsettings.Development.json`):**

```powershell
$env:ConnectionStrings__RabbitMq  = "amqp://appuser:MyDevPassword123!@localhost:5672/"
$env:ConnectionStrings__Redis     = "localhost:6379"
$env:ConnectionStrings__Elasticsearch = "http://elastic:Dev_P@ssw0rd1!@localhost:9200"
$env:KeyCloak__BaseUrl            = "http://localhost:8080"
$env:KeyCloak__M2M__ClientId      = "product-manager-function"
$env:KeyCloak__M2M__ClientSecrets__client-a = "dev-secret"
$env:KeyCloak__M2M__ClientSecrets__client-b = "dev-secret"
```

**Optional — override the API endpoint instead of Aspire service discovery:**
```powershell
$env:ProductManagerApi__BaseUrl = "http://localhost:5200"
```

**Optional — omit to skip Consul/Vault config loading:**
```powershell
$env:Consul__Url    = "http://localhost:8500"
$env:Vault__Address = "http://localhost:8200"
$env:Vault__Token   = "dev-only-token"
```

Then:
```powershell
cd src/ProductManager/OpenHub.ProductManager.ElasticProcessor
dotnet run
```

---

## Docker

Build the image (from the repo root):
```bash
docker build \
  -f src/ProductManager/OpenHub.ProductManager.ElasticProcessor/Dockerfile \
  -t openhub/product-manager-elastic-processor:latest \
  src/
```

Run:
```bash
docker run \
  -e ConnectionStrings__RabbitMq="amqp://appuser:MyDevPassword123!@host.docker.internal:5672/" \
  -e ConnectionStrings__Redis="host.docker.internal:6379" \
  -e KeyCloak__BaseUrl="http://host.docker.internal:8080" \
  -e KeyCloak__M2M__ClientId="product-manager-function" \
  -e KeyCloak__M2M__ClientSecrets__client-a="dev-secret" \
  -e ProductManagerApi__BaseUrl="http://host.docker.internal:8080" \
  openhub/product-manager-elastic-processor:latest
```

---

## M2M authentication

Uses the same per-tenant Client Credentials pattern documented in `ProductManager.Api`'s README ("M2M authentication (Client Credentials)"), via the shared `IKeyCloakM2MTokenAccessor` in `OpenHub.Accessor.KeyCloak` — not reimplemented here.

Each tenant realm (`client-a`, `client-b` in dev) has a `product-manager-function` service-account client with:
- The same `tenant-id` hardcoded-claim mapper as the tenant's other clients
- The same `product-manager-api-audience` mapper, so tokens pass `ProductManager.Api`'s `Auth:Audience` check

The client id is `product-manager-function`, not `product-manager-elastic-processor` — it's a deliberate carry-over from this consumer's previous identity as `OpenHub.ProductManager.Function`, so the already-provisioned Keycloak client and Vault secrets didn't need to be re-created under a new name. Same reasoning applies to the Consul config path (`product-manager-function/config`) and Vault secret path (`product-manager-function`) read in `Program.cs`.

`ProductChangedEvent.RealmSlug` tells the worker which realm to request a token from — no separate tenant-to-realm registry needed. Tokens are cached via `HybridCache`, so multiple worker instances share cached tokens instead of each re-authenticating.

To add a new tenant, mirror the `product-manager-function` client in that tenant's realm JSON (see `client-a-realm.json` / `client-b-realm.json`) and add its secret under `KeyCloak:M2M:ClientSecrets:{realm-slug}`.

---

## Messaging (Rebus + RabbitMQ)

The existing publisher (`OpenHub.Accessor.RabbitMq`'s `RabbitMqMessagePublisher`, used by `ProductManager.Api`) writes plain `System.Text.Json` bytes with only a `content-type: application/json` header — it predates Rebus and sets no `rbs2-*` headers, which Rebus's stock JSON deserializer normally requires to know which CLR type to deserialize into. `Messaging/Serialization/StampMessageTypeHeaderStep.cs` bridges this gap: an `IIncomingStep` inserted before Rebus's `DeserializeIncomingMessageStep` that stamps the missing headers, since this queue only ever carries `ProductChangedEvent` messages.

Retry policy: first-level retries only (`SimpleRetryStrategy`, 5 delivery attempts), no second-level/deferred backoff yet — the old Function's `host.json` had no retry config at all, so this is a strict improvement rather than a change in behavior. Messages that exhaust retries land in the configured error queue, same as any Rebus error queue.

**Topology on startup:** `Messaging/RabbitMqTopologyInitializer.cs` is an `IHostedService` registered before `AddRebus`, so it runs first — it idempotently declares the `Messaging:ExchangeName` fanout exchange (durable), declares `Messaging:QueueName` (durable), and binds them, every time the worker starts. This makes the worker self-sufficient: it no longer depends solely on the dev-only Aspire mechanism (`RabbitMq/definitions.json`, bind-mounted into the broker container) for correct topology, which matters for any environment that provisions the broker differently — e.g. a future Kubernetes deployment.

---

## Elasticsearch indexing

Products are written to one shared `products` index rather than one index per tenant. Tenant isolation is enforced the same way this codebase enforces it in Postgres (`FORCE ROW LEVEL SECURITY`, see `Database/ProductManager/V1_5__create_rls_policies.sql`) — at the data-access boundary, not by trusting the caller. The mechanics (index bootstrap, per-tenant filtered aliases, API-key issuance) live in the shared `OpenHub.Accessor.ElasticIndex` project (`ElasticIndexAccessor<TDocument>`), not in this project — `ProductDocument` (`Accessors/Models/ProductDocument.cs`) is the only ElasticProcessor-owned piece, implementing that library's `ITenantScopedDocument` contract. See that project's README for the full design (filtered aliases, API-key-only authentication past startup, the writer/reader key split, and the derived-API-key restriction that shapes it).

- **Per-tenant filtered alias** — `products-tenant-{tenantId}`, defined with a `term` filter on the document's `tenant_id` field. Anything querying through that alias only ever sees that tenant's documents.
- **Per-tenant reader API key** — restricted, by Elasticsearch role descriptor, to `read` on only that tenant's alias. A future search/read consumer retrieves this key from Vault to query Elasticsearch — it is physically incapable of reading another tenant's alias or the raw `products` index, enforced by Elasticsearch itself.

Both the alias and reader key are created lazily, on the first `ProductChangedEvent` seen for a given tenant (`IElasticIndexAccessor<ProductDocument>.EnsureTenantProvisionedAsync`, called from `ProductSyncManager`), and the "already ensured" check is `HybridCache`-backed so this isn't a network+Vault round-trip on every message.

Document `_id` is the product's own GUID, so repeated `ProductChangedEvent`s for the same product upsert rather than duplicate.

---

## Configuration reference

| Key | Source | Description |
|---|---|---|
| `ConnectionStrings:RabbitMq` | Aspire (`.WithReference(rabbitmq)`) / env | RabbitMQ connection string Rebus's transport connects with |
| `ConnectionStrings:Redis` | Aspire (`.WithReference(redis)`) / env | Redis connection string backing the M2M token accessor's `HybridCache` |
| `ConnectionStrings:Elasticsearch` | Aspire (`.WithReference(elasticsearch)`) / env | Elasticsearch superuser connection string — used only to bootstrap the writer API key (see "Elasticsearch indexing" above) |
| `Messaging:ExchangeName` | env / Consul | Fanout exchange declared and bound to on startup. Default `product.changed`. |
| `Messaging:QueueName` | env / Consul | Durable queue this worker consumes from. Default `product-manager-function.product-changed` (a naming artifact carried over from this consumer's previous identity — see "M2M authentication" below). |
| `Messaging:ErrorQueueName` | env / Consul | Where messages land after exhausting delivery attempts. Default `product-manager-elastic-processor.error`. |
| `KeyCloak:BaseUrl` | env / Consul | Keycloak base URL for M2M token requests |
| `KeyCloak:M2M:ClientId` | env / Consul | M2M client ID, same across all tenant realms |
| `KeyCloak:M2M:ClientSecrets:{realmSlug}` | env / Vault | Per-tenant-realm client secret |
| `ProductManagerApi:BaseUrl` | env | Overrides Aspire service discovery for standalone runs |
| `Consul:Url` | env | Consul agent URL. Omit to skip Consul config loading. |
| `Vault:Address` / `Vault:Token` | env | Vault server URL/token. Required (not just optional) for this worker — the writer and per-tenant reader Elasticsearch API keys are persisted here. |

---

## Architecture

```
Messaging/             ← Rebus wiring + trigger entry point (IHandleMessages<ProductChangedEvent>)
    ↓ calls
Managers/              ← orchestrate: fetch via Accessors, decide via Engines
    ↓ calls
Managers/Engines/       ← pure logic, no I/O (e.g. should a delete event fetch anything?)
Accessors/              ← all I/O: OpenHub.Accessor.KeyCloak (M2M tokens), ProductManager.Api (HTTP),
                          OpenHub.Accessor.ElasticIndex (tenant-isolated Elasticsearch indexing)
```

Same iDesign layering as `ProductManager.Api` — see `.claude/commands/idesign.md` for the full pattern rules. `Managers/`, `Managers/Engines/`, and `Accessors/` are carried over unchanged from `OpenHub.ProductManager.Function` (they were never Functions-specific); only the trigger entry point changed from a `[RabbitMQTrigger]`-decorated Azure Function to a Rebus `IHandleMessages<T>` handler.
