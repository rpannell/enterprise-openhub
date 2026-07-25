using Asp.Versioning;
using FluentValidation;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.ProductManager.Api;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Setup;
using OpenHub.ProductManager.Api.Controllers;
using OpenHub.ProductManager.Api.Managers.Engines.Setup;
using OpenHub.ProductManager.Api.Managers.Setup;
using OpenHub.ProductManager.Api.Mappers.Setup;
using OpenHub.ProductManager.Api.Managers.Options;
using OpenHub.ProductManager.Api.Middleware;
using OpenHub.Accessor.RabbitMq;

var builder = WebApplication.CreateBuilder(args);

// ── External configuration — system-wide first, service-specific second ───────
// Skipped silently if Consul:Url or Vault:Address env vars are not set. Must run before
// AddServiceDefaults() so Otel:CollectorEndpoint (and any other Consul-sourced config) is
// visible to it — builder.Configuration is a live ConfigurationManager, so sources added later
// aren't seen by eager reads done during AddServiceDefaults().
builder.AddConsulConfiguration(
    "platform/config",           // system-wide: logging levels, Keycloak URLs, feature flags
    "product-manager/config");   // service-specific overrides

builder.AddVaultSecrets(
    "platform",                  // system-wide secrets: shared creds, signing keys
    "product-manager");          // service-specific secrets: DB passwords, API keys

// ── Platform defaults (OpenTelemetry, health checks, service discovery, CORS) ─
builder.AddServiceDefaults();

// ── Database ─────────────────────────────────────────────────────────────────
builder.AddNpgsqlDbContext<CatalogDbContext>("Catalog");

// ── Caching (HybridCache: L1 in-process + L2 Redis) ─────────────────────────
// See ApiConst.RedisInstanceName for why this must be set — product-manager-api and crm-api
// share the same Aspire Redis resource, and without a unique per-service prefix their
// identically-shaped cache keys (e.g. "tenant:id:{tenantId}") collide.
builder.AddRedisDistributedCache("Redis");
builder.Services.Configure<RedisCacheOptions>(options => options.InstanceName = ApiConst.RedisInstanceName);
builder.Services.AddHybridCache();

// ── Messaging (RabbitMQ durable fanout exchange for product change events) ──
builder.Services.AddRabbitMqPublisher(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<ProductEventOptions>(builder.Configuration.GetSection("Events:Product"));

// ── Health checks — infrastructure dependencies ───────────────────────────────
// DB and Redis health checks are registered automatically by the Aspire integrations above.
// Keycloak readiness check: reads URL from service discovery or configuration.
var keycloakUrl = builder.Configuration["ConnectionStrings:KeyCloak"]
    ?? builder.Configuration["KeyCloak:BaseUrl"];
if (!string.IsNullOrWhiteSpace(keycloakUrl))
{
    builder.Services.AddHealthChecks()
        .AddCheck("keycloak", new KeycloakHealthCheck(keycloakUrl), tags: ["ready"]);
}

// ── Authentication — multi-realm Keycloak ────────────────────────────────────
builder.AddOpenHubJwtAuthentication();

// "AdminScope" gates the platform-admin endpoints (AdminEndpoints.cs) — a distinct M2M
// client/realm from every per-tenant realm, carrying a "product-manager-admin" scope
// claim. Deliberately separate from the default (tenant-scoped) authorization applied
// elsewhere, since an admin token has no tenant_id claim for TenantMiddleware to resolve.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminScope", policy => policy.RequireClaim("scope", "product-manager-admin"));

// ── API versioning ────────────────────────────────────────────────────────────
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// ── Validation ────────────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── iDesign layer registrations ───────────────────────────────────────────────
// Each layer owns its own DI setup via a static Add<Layer>() extension method,
// so the layer can be extracted to its own DLL without touching Program.cs.
builder.Services.AddHttpContextAccessor();

// Keycloak Admin API (singleton — cached token, shared HTTP client)
builder.Services.AddKeyCloakAccessor(builder.Configuration);

builder.Services.AddAccessors();
builder.Services.AddEngines();
builder.Services.AddManagers();
builder.Services.AddMappers();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();
app.MapDefaultEndpoints();
app.MapOpenHubApiDocs("OpenHub Product Manager API v1");
app.MapProductEndpoints();
app.MapLookupEndpoints();
app.MapManufacturerEndpoints();
app.MapBrandEndpoints();
app.MapAdminEndpoints();

app.Run();

// Exposes the top-level-statement-generated Program class to WebApplicationFactory<Program> in
// the test project (which otherwise can't see it — top-level statements generate it `internal`).
public partial class Program;
