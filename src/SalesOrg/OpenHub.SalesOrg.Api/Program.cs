using Asp.Versioning;
using FluentValidation;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.SalesOrg.Api;
using OpenHub.SalesOrg.Api.Accessors.Data;
using OpenHub.SalesOrg.Api.Accessors.Setup;
using OpenHub.SalesOrg.Api.Controllers;
using OpenHub.SalesOrg.Api.Managers.Engines.Setup;
using OpenHub.SalesOrg.Api.Managers.Setup;
using OpenHub.SalesOrg.Api.Mappers.Setup;
using OpenHub.SalesOrg.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ── Platform defaults (OpenTelemetry, health checks, service discovery, CORS) ─
builder.AddServiceDefaults();

// ── External configuration — system-wide first, service-specific second ───────
// Skipped silently if Consul:Url or Vault:Address env vars are not set.
builder.AddConsulConfiguration(
    "platform/config",       // system-wide: logging levels, Keycloak URLs, feature flags
    "salesorg/config");      // service-specific overrides

builder.AddVaultSecrets(
    "platform",  // system-wide secrets: shared creds, signing keys
    "salesorg"); // service-specific secrets: DB passwords, API keys

// ── Database ─────────────────────────────────────────────────────────────────
builder.AddNpgsqlDbContext<SalesOrgDbContext>("SalesOrg");

// ── Caching (HybridCache: L1 in-process + L2 Redis) ─────────────────────────
// See ApiConst.RedisInstanceName for why this must be set — crm-api, product-manager-api, and
// salesorg-api share the same Aspire Redis resource, and without a unique per-service prefix
// their identically-shaped cache keys (e.g. "tenant:id:{tenantId}") collide.
builder.AddRedisDistributedCache("Redis");
builder.Services.Configure<RedisCacheOptions>(options => options.InstanceName = ApiConst.RedisInstanceName);
builder.Services.AddHybridCache();

// ── Health checks — infrastructure dependencies ───────────────────────────────
// DB and Redis health checks are registered automatically by the Aspire integrations above.
var keycloakUrl = builder.Configuration["ConnectionStrings:KeyCloak"]
    ?? builder.Configuration["KeyCloak:BaseUrl"];
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
app.MapOpenHubApiDocs("OpenHub SalesOrg API v1");
app.MapSalesPersonEndpoints();
app.MapSalesRoleEndpoints();
app.MapSalesOrganizationUnitEndpoints();
app.MapSalesAssignmentEndpoints();

app.Run();

// Exposes the top-level-statement-generated Program class to WebApplicationFactory<Program> in
// the test project (which otherwise can't see it — top-level statements generate it `internal`).
public partial class Program;
