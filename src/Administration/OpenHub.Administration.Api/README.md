# OpenHub Administration API

Creates and manages organizations and users in Keycloak for the OpenHub platform. Unlike the
other OpenHub APIs, this project has **no database of its own** — every operation is a live
call against Keycloak's Admin REST API, and Keycloak is the single source of truth. There is
nothing here to cache that Keycloak can't answer directly.

## What this API does — and doesn't — do

- **Creates an organization** (`POST /api/v1/organizations`) inside an **already-provisioned**
  Keycloak realm. Realm creation itself (cloning the client-a/client-b realm-import shape for a
  brand-new tenant) is a manual ops step, not automated by this API — see
  `src/Platform/OpenHub.Platform.AppHost/Keycloak/`.
- **Creates users within an organization** (`POST /api/v1/organizations/{realmSlug}/users`),
  self-service, so an org's own `tenant-admin` can onboard their own teammates without OpenHub
  staff involvement.
- **Assigns realm roles**, with a built-in privilege-escalation guard: a `tenant-admin` can
  assign any ordinary role but can never grant `tenant-admin` or `platform-admin` to anyone —
  only a `platform-admin` can designate a new tenant-admin.

Every Keycloak Admin REST mutation this API makes (creating an organization, creating a user,
changing membership) fires Keycloak's own admin event, which the existing
`keycloak-rabbitmq-listener` fans out to the `tenant.changed`/`user.changed` RabbitMQ exchanges
exactly as it does for admin-console-driven changes — this API does not publish those events
itself.

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | `dotnet --version` |
| .NET Aspire workload | latest | `dotnet workload install aspire` |
| Docker Desktop | any | Required for all containerised dependencies |

---

## Running with Aspire (recommended for development)

```powershell
cd src
dotnet run --project Platform/OpenHub.Platform.AppHost
```

| Service | URL |
|---|---|
| Aspire Dashboard | http://localhost:18888 |
| API | Aspire-assigned (shown on dashboard) |
| API Docs (Scalar) | `{api-url}/api-docs` |
| Keycloak Admin | http://localhost:8080 |

## Running standalone (without Aspire)

**Required environment variables:**

```powershell
$env:KeyCloak__BaseUrl        = "http://localhost:8080"
$env:KeyCloak__AdminUsername  = "admin"
$env:KeyCloak__AdminPassword  = "<your-keycloak-admin-password>"
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
dotnet run --project Administration/OpenHub.Administration.Api
```

The API starts on `http://localhost:5300`. API docs at `/api-docs`.

---

## Authentication and authorization

Standard multi-realm JWT Bearer auth (`AddOpenHubJwtAuthentication()`) — the issuer is resolved
dynamically per-token, same as every other OpenHub API. On top of that, this API adds machinery
none of the others need, because its authorization decisions are role-based rather than the
single flat `scope` claim `ProductManager.Api`'s `AdminScope` policy checks:

**Keycloak delivers realm roles as a nested `realm_access: {"roles":[...]}` JSON claim** — ASP.NET
Core's JWT bearer handler does not flatten this into `ClaimTypes.Role` automatically.
`Auth/RealmRoleClaimsTransformation.cs` (an `IClaimsTransformation`) does that flattening once per
request so `ClaimsPrincipal.IsInRole(...)` and policy-based authorization work as expected.

Two authorization policies:

| Policy | Requirement | Gates |
|---|---|---|
| `PlatformAdminRole` | Realm role `platform-admin` **and** token issued by the `platform-admin` realm | `POST/GET /api/v1/organizations*` |
| `TenantAdminRole` | Realm role `tenant-admin` or `platform-admin` | `/api/v1/organizations/{realmSlug}/users*`, `/roles` |

The issuer check on `PlatformAdminRole` is defense in depth — the `platform-admin` role should
never exist in a tenant realm, but checking the issuer too means a stray role assignment there
still can't reach these endpoints.

`TenantAdminRole` only answers "does the caller hold an admin role at all" — it's coarse-grained
and route-agnostic. **Same-org enforcement** (a `tenant-admin` may only touch their own
`{realmSlug}`) happens in the Manager layer instead, built from the caller's own `iss`/`tenant_id`
claims (see `Extensions/ClaimsPrincipalExtensions.cs`) — a `platform-admin` bypasses this check
and can act on any organization.

### Setting up the platform-admin realm

`src/Platform/OpenHub.Platform.AppHost/Keycloak/platform-admin-realm.json` provisions this automatically
under Aspire, with a seed user (`ops-admin`) already holding the `platform-admin` role. In a
real deployment, create a dedicated `platform-admin` Keycloak realm (do not reuse a tenant
realm) with:

1. A `platform-admin` realm role.
2. A public client (`micro-ui-shell`, Authorization Code + PKCE) for OpenHub staff to log in
   interactively.
3. Staff user accounts with the `platform-admin` realm role assigned.

### Production audience validation

Set `Auth:Audience` (env: `Auth__Audience`) to enforce that only tokens explicitly targeting this
API are accepted, same as every other OpenHub API. This requires adding an audience mapper to the
relevant clients in both the `platform-admin` realm and every tenant realm — not configured by
default in this repo's dev realm-import JSON.

---

## Health checks

| Endpoint | Tags | Description |
|---|---|---|
| `GET /health` | all | All registered checks (Keycloak only — no DB/cache/messaging) |
| `GET /alive` | `live` | Liveness only — is the process up? |
| `GET /metrics` | — | Prometheus scrape endpoint |

---

## API endpoints

All routes are versioned under `/api/v{version}/`.

| Method | Path | Policy | Description |
|---|---|---|---|
| `POST` | `/api/v1/organizations` | `PlatformAdminRole` | Create an organization inside an already-provisioned realm |
| `GET` | `/api/v1/organizations` | `PlatformAdminRole` | List every organization across every provisioned tenant realm |
| `GET` | `/api/v1/organizations/{realmSlug}` | `PlatformAdminRole` | Get the organization for a given realm |
| `GET` | `/api/v1/organizations/{realmSlug}/users` | `TenantAdminRole` | List users in an organization |
| `POST` | `/api/v1/organizations/{realmSlug}/users` | `TenantAdminRole` | Create a user in an organization |
| `GET` | `/api/v1/organizations/{realmSlug}/roles` | `TenantAdminRole` | List realm roles the caller may assign |
| `POST` | `/api/v1/organizations/{realmSlug}/users/{userId}/roles` | `TenantAdminRole` | Assign a realm role to a user |

Full request/response schemas are in the Scalar UI at `/api-docs`.

---

## Architecture

```
Controllers (Endpoints)
    ↓ calls
Managers          ← orchestrate: fetch/mutate via the shared Keycloak accessors,
                     enforce same-org and role-escalation rules
    ↓ calls
Engines           ← pure logic (privilege-escalation guard), no I/O, fully unit-testable
Accessors         ← none locally — all Keycloak I/O lives in the shared
                     OpenHub.Accessor.KeyCloak library (IKeyCloakRealmAccessor,
                     IKeyCloakOrganizationAccessor, IKeyCloakUserAccessor), since it's
                     raw Keycloak Admin REST wrapping with no Administration-specific
                     business logic
```

No `TenantMiddleware`, no database, no Flyway migrations — there's no local tenant/user
row to provision or query. See `.claude/commands/idesign.md` for the full iDesign pattern
rules used across the platform.
