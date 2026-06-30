using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Setup;
using OpenHub.ProductManager.Api.Controllers;
using OpenHub.ProductManager.Api.Managers.Engines.Setup;
using OpenHub.ProductManager.Api.Managers.Setup;
using OpenHub.ProductManager.Api.Mappers.Setup;
using OpenHub.ProductManager.Api.Middleware;

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
// Dynamic issuer: reads the `iss` claim from the raw JWT before validation and sets
// Authority to that realm URL. This allows any tenant's realm to authenticate —
// both interactive users (auth-code flow) and M2M callers (client credentials).
//
// Audience: Set Auth:Audience in config (e.g. "product-manager") to enforce that
// only tokens explicitly targeting this API are accepted. Requires a Keycloak
// audience mapper on every client (user and M2M) in every tenant realm.
// Leave unset in dev to skip audience validation.
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

builder.Services.AddAuthorization();

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

app.Run();
