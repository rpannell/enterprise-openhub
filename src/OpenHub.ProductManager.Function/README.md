# OpenHub Product Manager Function

Azure Function (isolated worker, deployed as a Docker container) that reacts to product changes published by `OpenHub.ProductManager.Api`. First consumer of the `product.changed` RabbitMQ exchange.

## What it does

| Trigger | Function | Behavior |
|---|---|---|
| RabbitMQ queue `product-manager-function.product-changed` | `ProductChangedQueueFunction` | Deserializes a `ProductChangedEvent`, gets an M2M token for the event's tenant realm, fetches the product from `ProductManager.Api`, logs it. Elastic write is not implemented yet. |
| Timer, `%NightlyIndexSchedule%` | `NightlyIndexFunction` | Scaffold only — logs and returns. Tenant enumeration and the full Elastic re-index still need their own design pass. |

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | `dotnet --version` |
| .NET Aspire workload | latest | `dotnet workload install aspire` |
| Azure Functions Core Tools | v4 | Needed for local Functions host (`func start`), which Aspire drives automatically |
| Docker Desktop | any | Required for all containerized dependencies, and for the deployed container image |

---

## Running with Aspire (recommended)

```powershell
cd src
dotnet run --project OpenHub.Platform.AppHost
```

Aspire starts RabbitMQ, Keycloak, Consul, Vault, Redis, `ProductManager.Api`, and this Function together, with service discovery wired up (`.WithReference(productManagerApi)` resolves the API's URL automatically — no hardcoded endpoint to keep in sync). Aspire runs the local Functions host (`func start`) for this project during `aspire run`; the Dockerfile is a separate path used only for building the deployed container image, the same relationship `ProductManager.Api` has with its own Dockerfile.

This Function has no HTTP triggers, so there's no browsable endpoint — watch its output via the Aspire dashboard (http://localhost:18888) instead.

To see it fire: create, update, or delete a product via `ProductManager.Api`, then check this Function's logs in the dashboard.

---

## Running standalone (without Aspire)

**Required environment variables (or `appsettings.Development.json`):**

```powershell
$env:RabbitMq__HostName          = "localhost"
$env:RabbitMq__Port              = "5672"
$env:RabbitMq__UserName          = "appuser"
$env:RabbitMq__Password          = "MyDevPassword123!"
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
cd src/OpenHub.ProductManager.Function
func start
```

You'll also need `AzureWebJobsStorage` set in `local.settings.json` (gitignored — not committed) — `UseDevelopmentStorage=true` works with the Azurite storage emulator, or point it at a real storage account.

---

## Docker

Build the image (from the repo root):
```bash
docker build \
  -f src/OpenHub.ProductManager.Function/Dockerfile \
  -t openhub/product-manager-function:latest \
  src/
```

Run:
```bash
docker run \
  -e AzureWebJobsStorage="<storage-connection-string>" \
  -e RabbitMq__HostName="host.docker.internal" \
  -e RabbitMq__Port="5672" \
  -e RabbitMq__UserName="appuser" \
  -e RabbitMq__Password="MyDevPassword123!" \
  -e KeyCloak__BaseUrl="http://host.docker.internal:8080" \
  -e KeyCloak__M2M__ClientId="product-manager-function" \
  -e KeyCloak__M2M__ClientSecrets__client-a="dev-secret" \
  -e ProductManagerApi__BaseUrl="http://host.docker.internal:8080" \
  openhub/product-manager-function:latest
```

---

## M2M authentication

Uses the same per-tenant Client Credentials pattern documented in `ProductManager.Api`'s README ("M2M authentication (Client Credentials)"), via the shared `IKeyCloakM2MTokenAccessor` in `OpenHub.Accessor.KeyCloak` — not reimplemented here.

Each tenant realm (`client-a`, `client-b` in dev) has a `product-manager-function` service-account client with:
- The same `tenant-id` hardcoded-claim mapper as the tenant's other clients
- The same `product-manager-api-audience` mapper, so tokens pass `ProductManager.Api`'s `Auth:Audience` check

`ProductChangedEvent.RealmSlug` tells the Function which realm to request a token from — no separate tenant-to-realm registry needed. Tokens are cached via `HybridCache`, so multiple Function instances share cached tokens instead of each re-authenticating.

To add a new tenant, mirror the `product-manager-function` client in that tenant's realm JSON (see `client-a-realm.json` / `client-b-realm.json`) and add its secret under `KeyCloak:M2M:ClientSecrets:{realm-slug}`.

---

## Configuration reference

| Key | Source | Description |
|---|---|---|
| `RabbitMq:HostName` / `Port` / `UserName` / `Password` | env / Consul+Vault | Composed into the `RabbitMQConnection` app setting the trigger binding needs (the extension requires one full AMQP URI, not discrete settings) |
| `KeyCloak:BaseUrl` | env / Consul | Keycloak base URL for M2M token requests |
| `KeyCloak:M2M:ClientId` | env / Consul | M2M client ID, same across all tenant realms |
| `KeyCloak:M2M:ClientSecrets:{realmSlug}` | env / Vault | Per-tenant-realm client secret |
| `ProductManagerApi:BaseUrl` | env | Overrides Aspire service discovery for standalone runs |
| `NightlyIndexSchedule` | env / Consul | NCronTab schedule for `NightlyIndexFunction`, e.g. `0 0 2 * * *` (2am daily) |
| `Consul:Url` | env | Consul agent URL. Omit to skip Consul config loading. |
| `Vault:Address` / `Vault:Token` | env | Vault server URL/token. Omit to skip Vault secret loading. |

---

## Architecture

```
Functions/            ← trigger entry points (RabbitMQTrigger, TimerTrigger)
    ↓ calls
Managers/              ← orchestrate: fetch via Accessors, decide via Engines
    ↓ calls
Managers/Engines/       ← pure logic, no I/O (e.g. should a delete event fetch anything?)
Accessors/              ← all I/O: OpenHub.Accessor.KeyCloak (M2M tokens), ProductManager.Api (HTTP)
```

Same iDesign layering as `ProductManager.Api` — see `.claude/commands/idesign.md` for the full pattern rules.
