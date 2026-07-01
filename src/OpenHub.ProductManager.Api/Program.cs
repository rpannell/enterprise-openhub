using Asp.Versioning;
using FluentValidation;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Setup;
using OpenHub.ProductManager.Api.Controllers;
using OpenHub.ProductManager.Api.Managers.Engines.Setup;
using OpenHub.ProductManager.Api.Managers.Setup;
using OpenHub.ProductManager.Api.Mappers.Setup;
using OpenHub.ProductManager.Api.Middleware;
using OpenHub.Accessor.RabbitMq;

var builder = WebApplication.CreateBuilder(args);

// ── Platform defaults (OpenTelemetry, health checks, service discovery, CORS) ─
builder.AddServiceDefaults();

// ── External configuration — system-wide first, service-specific second ───────
// Skipped silently if Consul:Url or Vault:Address env vars are not set.
builder.AddConsulConfiguration(
    "platform/config",           // system-wide: logging levels, Keycloak URLs, feature flags
    "product-manager/config");   // service-specific overrides

builder.AddVaultSecrets(
    "platform",                  // system-wide secrets: shared creds, signing keys
    "product-manager");          // service-specific secrets: DB passwords, API keys

// ── Database ─────────────────────────────────────────────────────────────────
builder.AddNpgsqlDbContext<CatalogDbContext>("catalog");

// ── Caching (HybridCache: L1 in-process + L2 Redis) ─────────────────────────
builder.AddRedisDistributedCache("redis");
builder.Services.AddHybridCache();

// ── Messaging (RabbitMQ durable fanout exchange for product change events) ──
builder.Services.AddRabbitMqPublisher(builder.Configuration.GetSection("RabbitMq"));

// ── Health checks — infrastructure dependencies ───────────────────────────────
// DB and Redis health checks are registered automatically by the Aspire integrations above.
// Keycloak readiness check: reads URL from service discovery or configuration.
var keycloakUrl = builder.Configuration["ConnectionStrings:keycloak"]
    ?? builder.Configuration["KeyCloak:Url"];
if (!string.IsNullOrWhiteSpace(keycloakUrl))
{
    builder.Services.AddHealthChecks()
        .AddCheck("keycloak", new KeycloakHealthCheck(keycloakUrl), tags: ["ready"]);
}

// ── Authentication — multi-realm Keycloak ────────────────────────────────────
builder.AddOpenHubJwtAuthentication();

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

app.Run();
