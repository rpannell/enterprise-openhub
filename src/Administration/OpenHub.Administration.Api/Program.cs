using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.Administration.Api.Auth;
using OpenHub.Administration.Api.Controllers;
using OpenHub.Administration.Api.Managers.Engines.Setup;
using OpenHub.Administration.Api.Managers.Setup;
using OpenHub.Administration.Api.Mappers.Setup;

var builder = WebApplication.CreateBuilder(args);

// ── External configuration — system-wide first, service-specific second ───────
// Skipped silently if Consul:Url or Vault:Address env vars are not set. Must run before
// AddServiceDefaults() so Otel:CollectorEndpoint (and any other Consul-sourced config) is
// visible to it — builder.Configuration is a live ConfigurationManager, so sources added later
// aren't seen by eager reads done during AddServiceDefaults().
builder.AddConsulConfiguration(
    "platform/config",            // system-wide: logging levels, Keycloak URLs, feature flags
    "administration/config");     // service-specific overrides

builder.AddVaultSecrets(
    "platform",                   // system-wide secrets: shared creds, signing keys
    "administration");            // service-specific secrets: Keycloak admin password

// ── Platform defaults (OpenTelemetry, health checks, service discovery, CORS) ─
builder.AddServiceDefaults();

// ── Health checks — infrastructure dependencies ───────────────────────────────
// No database/cache/messaging here — this API is a stateless facade over Keycloak's
// Admin REST API, so Keycloak readiness is the only infrastructure dependency.
var keycloakUrl = builder.Configuration["ConnectionStrings:KeyCloak"]
    ?? builder.Configuration["KeyCloak:BaseUrl"];
if (!string.IsNullOrWhiteSpace(keycloakUrl))
{
    builder.Services.AddHealthChecks()
        .AddCheck("keycloak", new KeycloakHealthCheck(keycloakUrl), tags: ["ready"]);
}

// ── Authentication — multi-realm Keycloak ────────────────────────────────────
builder.AddOpenHubJwtAuthentication();

// Keycloak delivers realm roles as a nested realm_access.roles JSON claim — flatten it
// into ClaimTypes.Role claims so the policies below (and ClaimsPrincipal.IsInRole) work.
builder.Services.AddSingleton<IClaimsTransformation, RealmRoleClaimsTransformation>();

builder.Services.AddSingleton<IAuthorizationHandler, PlatformAdminHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, TenantAdminHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PlatformAdminRole", policy => policy.AddRequirements(new PlatformAdminRequirement()))
    .AddPolicy("TenantAdminRole", policy => policy.AddRequirements(new TenantAdminRequirement()));

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

// Keycloak Admin API (typed clients — cached admin token shared via KeyCloakAdminTokenProvider)
builder.Services.AddKeyCloakAccessor(builder.Configuration);

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
// No TenantMiddleware — there's no local tenant/user row to provision. Realm and
// tenant identity are read straight off the caller's JWT per-request instead
// (see Extensions/ClaimsPrincipalExtensions.cs).
app.MapDefaultEndpoints();
app.MapOpenHubApiDocs("OpenHub Administration API v1");
app.MapOrganizationEndpoints();
app.MapUserEndpoints();

app.Run();

// Exposes the top-level-statement-generated Program class to WebApplicationFactory<Program> in
// the test project (which otherwise can't see it — top-level statements generate it `internal`).
public partial class Program;
