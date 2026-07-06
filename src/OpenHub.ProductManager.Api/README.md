# OpenHub Product Manager API

Manages the product catalog for the OpenHub platform. Multi-tenant via Keycloak realms and PostgreSQL Row Level Security.

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | `dotnet --version` |
| .NET Aspire workload | latest | `dotnet workload install aspire` |
| Docker Desktop | any | Required for all containerised dependencies |

---

## Running with Aspire (recommended for development)

Aspire starts Postgres, Redis, Keycloak, RabbitMQ, Consul, Vault, Flyway, and the API automatically with service discovery wired up.

```powershell
cd src
dotnet run --project OpenHub.Platform.AppHost
```

| Service | URL |
|---|---|
| Aspire Dashboard | http://localhost:18888 |
| API | Aspire-assigned (shown on dashboard) |
| API Docs (Scalar) | `{api-url}/api-docs` |
| OpenAPI spec | `{api-url}/openapi/v1.json` |
| Keycloak Admin | http://localhost:8080 |

**Keycloak admin password** is prompted by Aspire on first run (stored in user secrets).

---

## Running standalone (without Aspire)

Useful for quick iteration when the full Aspire stack isn't needed.

**Required environment variables:**

```powershell
$env:ConnectionStrings__Catalog  = "Host=localhost;Database=catalog;Username=postgres;Password=Dev_P@ssw0rd1!"
$env:ConnectionStrings__Redis    = "localhost:6379"
$env:KeyCloak__BaseUrl           = "http://localhost:8080"
$env:KeyCloak__AdminUsername     = "admin"
$env:KeyCloak__AdminPassword     = "<your-keycloak-admin-password>"
```

**Optional — omit to skip Keycloak readiness check:**
```powershell
$env:ConnectionStrings__KeyCloak = "http://localhost:8080"
```

**Optional — omit to skip Consul/Vault config loading:**
```powershell
$env:Consul__Url     = "http://localhost:8500"
$env:Vault__Address  = "http://localhost:8200"
$env:Vault__Token    = "dev-only-token"
```

Then run:
```powershell
cd src
dotnet run --project OpenHub.ProductManager.Api
```

The API starts on `http://localhost:5100`. API docs at `/api-docs`.

---

## Docker

Build the image (from the repo root):
```bash
docker build \
  -f src/OpenHub.ProductManager.Api/Dockerfile \
  -t openhub/product-manager:latest \
  src/
```

Run:
```bash
docker run -p 8080:8080 \
  -e ConnectionStrings__Catalog="Host=host.docker.internal;Database=catalog;Username=postgres;Password=Dev_P@ssw0rd1!" \
  -e ConnectionStrings__Redis="host.docker.internal:6379" \
  -e KeyCloak__BaseUrl="http://host.docker.internal:8080" \
  -e KeyCloak__AdminUsername="admin" \
  -e KeyCloak__AdminPassword="<password>" \
  openhub/product-manager:latest
```

---

## Authentication

### How it works

All endpoints require a JWT Bearer token issued by Keycloak. The API is **multi-realm**: each tenant has their own Keycloak realm, and the issuer is resolved dynamically from the `iss` claim in the token. This means a single API instance serves all tenants.

```
Client → [Bearer token: iss=.../realms/tenant-a] → API → validate against tenant-a's JWKS
Client → [Bearer token: iss=.../realms/tenant-b] → API → validate against tenant-b's JWKS
```

After token validation, `TenantMiddleware` extracts the realm slug from the issuer URL and resolves (or auto-provisions) the tenant record in the database.

### User authentication (Authorization Code + PKCE)

Used by the frontend shell (`micro-ui-shell` client in Keycloak).

```
POST /realms/{tenant-realm}/protocol/openid-connect/token
grant_type=authorization_code&code=...&client_id=micro-ui-shell
```

### M2M authentication (Client Credentials)

Used by backend services calling this API programmatically.

```bash
curl -X POST "https://{keycloak}/realms/{tenant-realm}/protocol/openid-connect/token" \
  -d "grant_type=client_credentials" \
  -d "client_id={your-service-client-id}" \
  -d "client_secret={your-client-secret}"
```

The resulting token has:
- `iss` = `https://{keycloak}/realms/{tenant-realm}` — identifies which tenant
- `sub` = the service account UUID
- `aud` = `["product-manager"]` (requires audience mapper — see below)

### Audience validation

Set `Auth:Audience` (env: `Auth__Audience`) to enforce that only tokens explicitly targeting this API are accepted. **Recommended for production.**

```powershell
$env:Auth__Audience = "product-manager"
```

Without this, any valid Keycloak token from any realm is accepted regardless of intended audience.

### Keycloak configuration for M2M per tenant

Each tenant realm that needs M2M access must have:

1. A client with **Service Accounts Enabled** (Client Credentials grant type)
2. An **audience mapper** that adds `product-manager` to the `aud` claim:
   - Clients → `{your-m2m-client}` → Client scopes → (dedicated scope) → Mappers → Create
   - Mapper Type: **Audience**
   - Included Custom Audience: `product-manager`

Repeat for each tenant realm. The same mapper configuration applies to the user-facing client if `Auth:Audience` is set (to ensure interactive tokens also carry the audience).

### Per-tenant M2M and multiple audiences

Each tenant's M2M client in their own realm issues tokens with:
- Different `iss` (realm URL) → identifies which tenant's data to access
- Same `aud` (`product-manager`) → always targets this API

The audience does **not** vary per tenant — it identifies the target service, not the caller. Different tenants call the same API using tokens from their own realm.

If a service needs to call multiple APIs (e.g., both `product-manager` and `inventory-manager`), configure the Keycloak audience mapper to include multiple values, or obtain separate tokens per API.

---

## Configuration reference

All configuration keys follow `Section:Key` naming (env var: `Section__Key`).

| Key | Source | Description |
|---|---|---|
| `ConnectionStrings:Catalog` | Aspire / env | PostgreSQL connection string |
| `ConnectionStrings:Redis` | Aspire / env | Redis connection string |
| `ConnectionStrings:KeyCloak` | Aspire / env | Keycloak base URL (for health check) |
| `KeyCloak:BaseUrl` | env / Consul | Keycloak base URL for admin API calls |
| `KeyCloak:AdminUsername` | env | Keycloak admin username |
| `KeyCloak:AdminPassword` | env / Vault | Keycloak admin password |
| `Auth:Audience` | env | JWT audience to validate (`product-manager`). Omit to skip audience validation. |
| `Consul:Url` | env | Consul agent URL. Omit to skip Consul config loading. |
| `Vault:Address` | env | Vault server URL. Omit to skip Vault secret loading. |
| `Vault:Token` | env / Vault | Vault authentication token. |
| `Cors:AllowedOrigins` | Consul / env | Allowed CORS origins (array). Dev fallback allows any origin. |

---

## Health checks

| Endpoint | Tags | Description |
|---|---|---|
| `GET /health` | all | All registered checks (DB, Redis, Keycloak) |
| `GET /alive` | `live` | Liveness only — is the process up? |
| `GET /metrics` | — | Prometheus scrape endpoint |

---

## API endpoints

All routes are versioned under `/api/v{version}/`.

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/v1/products` | List all products for the tenant |
| `GET` | `/api/v1/products/{id}` | Get product by ID |
| `POST` | `/api/v1/products` | Create a product |
| `PUT` | `/api/v1/products/{id}` | Update a product |
| `DELETE` | `/api/v1/products/{id}` | Soft-delete a product |

Full request/response schemas are in the Scalar UI at `/api-docs`.

---

## Database migrations

Schema is managed by **Flyway**, not EF Core. EF Core is used only for querying.

Migrations live in `src/Database/ProductManager/`. Aspire runs Flyway automatically before the API starts. To run manually against a local Postgres:

```bash
flyway -url=jdbc:postgresql://localhost:5432/catalog \
       -user=postgres \
       -password=Dev_P@ssw0rd1! \
       -schemas=ref,catalog \
       -locations=filesystem:src/Database/ProductManager \
       migrate
```

---

## Architecture

```
Controllers (Endpoints)
    ↓ calls
Managers          ← orchestrate: fetch via Accessors, transform via Engines
    ↓ calls
Engines           ← pure domain logic, no I/O, fully unit-testable
Accessors         ← all I/O: EF Core (DB), HybridCache (Redis+in-process)
```

See `.claude/commands/idesign.md` for the full iDesign pattern rules used in this project.
