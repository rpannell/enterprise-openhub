# OpenHub Platform Conventions

Cross-cutting standards for every API in the OpenHub platform, plus the one frontend-architecture
decision below. Apply the API conventions after scaffolding with `/idesign`.

## Frontend modules: local folder vs. Module Federation remote

Every business module (Product Management, CRM, Help Desk, Ordering, Administration) defaults to
a **local folder** in `OpenHub.UI/src/modules/<name>` — one Vite build, lazy-loaded, gated by role
in `src/config/modules.ts`. Only split a module into its own Module Federation remote when it
actually needs independent deploys (a different team, a different release schedule) — that's real
infra cost (shared dependency versions, a federation host config, more CI/CD surfaces, more
runtime failure modes), not something to default into.

**Administration is the one federated remote today** (`src/Administration/OpenHub.Administration.UI`,
built with Rspack, not Vite) — a genuine worked example of the pattern, including every real
failure hit building it. Before federating the next module (CRM, Help Desk, and Ordering are the
current local-folder placeholders in `modules.ts` most likely to need this next), read:

- `OpenHub.UI/README.md`'s "Architecture" section — the decision framing (why not federate
  everything by default)
- `OpenHub.UI/docs/module-federation.md` — the full walkthrough: host/remote config shape, the
  `HostAuthContext` contract (how auth crosses the federation boundary as a plain prop, not a
  shared React context), every CORS/container-format/router-sharing/dev-server failure mode
  already hit and fixed, and "What's simplified here" — what a real multi-remote platform needs
  that this one-remote example deliberately doesn't have yet (a published contracts package, a
  shared design-system remote, independent CI/CD, contract-compatibility testing)

**Folder placement**: a new remote gets its own area folder under `src/`, matching
`src/Administration/OpenHub.Administration.UI` — e.g. a federated CRM module would be
`src/CRM/OpenHub.CRM.UI`, sibling to `src/CRM/OpenHub.CRM.Api` if that module's backend also gets
split out of `OpenHub.ProductManager.Api`/its own API at the same time. See the `src/` area-folder
reorg (`Accessor/`, `Administration/`, `Platform/`, `ProductManager/`, `Shell/`) for the pattern.

**Frontend unit testing: Vitest.** Not yet set up in any project as of this writing (no
`vitest` dependency or test script in either `OpenHub.UI` or `OpenHub.Administration.UI`'s
`package.json` yet) — noted here as the decided tool so whoever scaffolds the first test suite
doesn't have to re-litigate the choice. Natural fit alongside the existing Vite tooling in the
shell; works the same way for the Rspack-built Administration remote (Vitest doesn't require Vite
itself, just shares its config/transform pipeline conventions).

## OpenAPI Documentation

Every endpoint and every public model property must be documented.

### Endpoint metadata

```csharp
group.MapGet("/{id:guid}", async (...) => { ... })
    .WithName("GetProductById")
    .WithSummary("Get a product by ID")
    .WithDescription("Returns the product belonging to the caller's tenant. Returns 404 if the product does not exist or belongs to a different tenant.")
    .Produces<ProductResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);
```

**Standard status codes by verb:**

| Method | Success | Also declare |
|---|---|---|
| GET (collection) | 200 | — |
| GET (by ID) | 200 | 404 |
| POST | 201 | 422 (`.ProducesValidationProblem()` if validated) |
| PUT | 200 | 404, 422 (if validated) |
| DELETE | 204 | 404 |

### Model properties

Use `[Description]` (from `System.ComponentModel`) on every request and response property:

```csharp
public class CreateProductRequest
{
    [Description("Stock-keeping unit. Must be unique within the tenant. Max 100 characters.")]
    public string Sku { get; set; } = null!;

    [Description("Display name shown to end users. Max 500 characters.")]
    public string Name { get; set; } = null!;
}
```

## Validation

Register validators from the assembly at startup:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```

Apply to endpoints with the filter from `OpenHub.Platform.ServiceDefaults`:

```csharp
.AddEndpointFilter<ValidationFilter<CreateProductRequest>>()
.ProducesValidationProblem()
```

Validator naming: `Create<Entity>RequestValidator`, `Update<Entity>RequestValidator`.
Update validators must treat all rules as conditional (`When(x => x.Field != null, () => ...)`).

## Authentication

JWT Bearer via Keycloak. Dynamic multi-realm: the `iss` claim is read from the raw JWT before
validation and used as the `Authority`. Any tenant's Keycloak realm authenticates automatically —
no restart or config change required when a new realm is added.

### Program.cs wiring

```csharp
var jwtAudience = builder.Configuration["Auth:Audience"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        if (!string.IsNullOrWhiteSpace(jwtAudience))
            options.Audience = jwtAudience;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Token is null) return Task.CompletedTask;
                try
                {
                    var handler = new JsonWebTokenHandler();
                    var jwt = handler.ReadJsonWebToken(ctx.Token);
                    var iss = jwt.Issuer;
                    if (!string.IsNullOrEmpty(iss))
                    {
                        ctx.Options.Authority = iss;
                        ctx.Options.TokenValidationParameters.ValidIssuer = iss;
                    }
                }
                catch { }
                return Task.CompletedTask;
            }
        };
    });
```

### M2M (Client Credentials) setup in Keycloak

For each tenant realm that needs M2M access:
1. Create a client with **Service Accounts Enabled**
2. Add an **Audience** mapper → Included Custom Audience: `product-manager` (or the target API's name)
3. The token will have `iss = .../realms/{tenant}` and `aud = ["product-manager"]`

Set `Auth__Audience=product-manager` in production to enforce audience validation.

### Middleware ordering

```csharp
app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();  // must be after auth — reads User claims
```

`CorrelationIdMiddleware` runs automatically (registered via `IStartupFilter` in `AddServiceDefaults`).

## CORS

Configured in `AddServiceDefaults` via `AddOpenHubCors`. No per-API setup needed.

In production, set `Cors__AllowedOrigins__0=https://app.example.com` (and so on for each origin).
In development with no origins configured, any origin is allowed.

Call `app.UseCors()` in the middleware pipeline (before `UseAuthentication`).

## Health Checks

Register infrastructure dependencies at startup:

```csharp
var keycloakUrl = builder.Configuration["ConnectionStrings:KeyCloak"]
    ?? builder.Configuration["KeyCloak:BaseUrl"];
if (!string.IsNullOrWhiteSpace(keycloakUrl))
    builder.Services.AddHealthChecks()
        .AddCheck("keycloak", new KeycloakHealthCheck(keycloakUrl), tags: ["ready"]);
```

`KeycloakHealthCheck` is in `OpenHub.Platform.ServiceDefaults` (namespace `Microsoft.Extensions.Hosting`).
DB and Redis health checks are auto-registered by `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`
and `Aspire.StackExchange.Redis`.

Endpoints exposed by `MapDefaultEndpoints()`:
- `/health` — all checks
- `/alive` — liveness (`live` tag only)
- `/metrics` — Prometheus scrape

## Correlation ID

Automatic — no per-API setup required. `AddServiceDefaults` registers `CorrelationIdStartupFilter`
which injects `CorrelationIdMiddleware` first in the pipeline.

Behaviour:
- Reads `X-Correlation-Id` from request headers or generates a new UUID
- Echoes the ID in the response `X-Correlation-Id` header
- Tags the active OTel span with `correlation.id`
- Injects the ID into the log scope for every log line in that request

Access within a handler: `httpContext.Items["CorrelationId"] as string`

## Configuration & Secrets

**Consul** — shared, non-sensitive config. Key prefix: `{service-name}/config`.
Values reload at runtime without restart.

**Vault** — secrets (passwords, API keys). Path: `secret/data/{service-name}`.
Loaded once at startup; override appsettings values.

```csharp
builder.AddConsulConfiguration("platform/config", "product-manager/config");
builder.AddVaultSecrets("platform", "product-manager");
```

Both skip silently when `Consul__Url` / `Vault__Address` env vars are absent.
**Never put Consul or Vault URLs in `appsettings.json`** — omitting the env var is how standalone
dev mode runs without the full Aspire stack.

In Aspire, these env vars are injected automatically via `WithEnvironment(...)` in `AppHost.cs`.

## Documenting new configuration

Any time a new `appsettings`/environment-variable key is introduced (or a default changes), add
or update a "Configuration reference" table in that project's README in the same change — key
name, what it does, and its default. This is how anyone doing a deploy knows what needs to be set;
undocumented config is what turns into a production incident nobody can explain. Applies to
project-local settings (e.g. `ReindexJob:PageConcurrency`) and shared library settings read via
`IConfiguration` in an `Accessor`/`ServiceDefaults` project (e.g. `ElasticIndex:NumberOfShards`) —
document it in whichever README covers the code that reads it.

## HybridCache Pattern

`HybridCache` (L1 in-process + L2 Redis) is the only caching abstraction. No `IMemoryCache` or
`IDistributedCache` directly.

```csharp
private static readonly HybridCacheEntryOptions CacheOptions = new()
{
    Expiration = TimeSpan.FromHours(1),           // L2 Redis TTL
    LocalCacheExpiration = TimeSpan.FromMinutes(5) // L1 in-process TTL
};

var result = await cache.GetOrCreateAsync(
    $"tenant:id:{id}",
    async _ => await LoadFromDbAsync(id, ct),
    CacheOptions,
    cancellationToken: ct);
```

Cache key conventions:
- Tenant: `tenant:id:{guid}`
- Ref data: `ref:{table}` (e.g. `ref:manufacturers`)
- Invalidate: `await cache.RemoveAsync("ref:manufacturers")`

## Validate Mode

When the user types `/platform validate`, check:

1. **OpenAPI metadata** — every `MapGet/Post/Put/Delete` has `.WithName()`, `.WithSummary()`, `.WithDescription()`, and at least one `.Produces<T>()`
2. **Model descriptions** — every public property in `Models/Requests/` and `Models/Responses/` has `[Description(...)]`
3. **Auth:Audience** — warn if not configured (acceptable in dev, required in production)
4. **Keycloak health check** — `KeycloakHealthCheck` is registered in `AddHealthChecks()`
5. **Consul/Vault URLs in appsettings.json** — error if `Consul:Url` or `Vault:Address` appear in any `appsettings*.json`
6. **TenantMiddleware ordering** — `UseMiddleware<TenantMiddleware>()` appears after `UseAuthorization()` in `Program.cs`
7. **ValidationFilter applied** — POST and PUT endpoints with a request body have `.AddEndpointFilter<ValidationFilter<T>>()`

Report each issue with file path and line number.
