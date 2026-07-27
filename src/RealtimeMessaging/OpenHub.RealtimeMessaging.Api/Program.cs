using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using OpenHub.RealtimeMessaging.Api;
using OpenHub.RealtimeMessaging.Api.Accessors.Setup;
using OpenHub.RealtimeMessaging.Api.Hubs;
using OpenHub.RealtimeMessaging.Api.Managers.Engines.Setup;
using OpenHub.RealtimeMessaging.Api.Managers.Setup;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── External configuration — system-wide first, service-specific second ───────
// Skipped silently if Consul:Url or Vault:Address env vars are not set. Must run before
// AddServiceDefaults() so Otel:CollectorEndpoint (and any other Consul-sourced config) is
// visible to it — builder.Configuration is a live ConfigurationManager, so sources added later
// aren't seen by eager reads done during AddServiceDefaults().
builder.AddConsulConfiguration(
    "platform/config",              // system-wide: logging levels, Keycloak URLs, feature flags
    "realtime-messaging/config");   // service-specific overrides

builder.AddVaultSecrets(
    "platform",                // system-wide secrets: shared creds, signing keys
    "realtime-messaging");     // service-specific secrets (none today — kept for convention parity)

// ── Platform defaults (OpenTelemetry, health checks, service discovery, CORS) ─
builder.AddServiceDefaults();

// ── Redis (SignalR scale-out backplane) ─────────────────────────────────────
// Aspire's .WithReference(redis) on this project injects ConnectionStrings:Redis. This service
// has no domain caching need of its own (unlike CRM/ProductManager's HybridCache usage), so this
// call exists purely to get the connection string + the standard Aspire health check/telemetry
// wiring other services already get for the same shared Redis instance.
builder.AddRedisDistributedCache("Redis");

builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!, options =>
    {
        // Nothing else does pub/sub on this shared Redis instance today, but this avoids a
        // future collision the same way each API's HybridCache RedisInstanceName prefix does.
        options.Configuration.ChannelPrefix = RedisChannel.Literal(ApiConst.RedisChannelPrefix);
    });

// Backs Clients.User(...) with a "{realmSlug}:{sub}" identifier — see RealmScopedUserIdProvider.
builder.Services.AddSingleton<IUserIdProvider, RealmScopedUserIdProvider>();

// ── Health checks — infrastructure dependencies ───────────────────────────────
// Redis health check is registered automatically by the Aspire integration above.
var keycloakUrl = builder.Configuration["ConnectionStrings:KeyCloak"]
    ?? builder.Configuration["KeyCloak:BaseUrl"];
if (!string.IsNullOrWhiteSpace(keycloakUrl))
{
    builder.Services.AddHealthChecks()
        .AddCheck("keycloak", new KeycloakHealthCheck(keycloakUrl), tags: ["ready"]);
}

// ── Authentication — multi-realm Keycloak ────────────────────────────────────
builder.AddOpenHubJwtAuthentication();

// SignalR's browser transports can't set an Authorization header on the WebSocket handshake —
// pull the token from ?access_token= instead, but only for requests actually targeting the hub,
// so every other endpoint on this API still requires a real Authorization header. Verified safe
// against MultiRealmJwtPostConfigureOptions: that only sets IssuerSigningKeyResolver/
// IssuerValidator and never touches Events, and runs via IPostConfigureOptions strictly after
// this Configure<JwtBearerOptions> delegate.
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var previousOnMessageReceived = options.Events?.OnMessageReceived;
    options.Events ??= new JwtBearerEvents();
    options.Events.OnMessageReceived = async context =>
    {
        if (previousOnMessageReceived is not null)
            await previousOnMessageReceived(context);

        if (string.IsNullOrEmpty(context.Token))
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) &&
                context.HttpContext.Request.Path.StartsWithSegments(ApiConst.HubRoute))
            {
                context.Token = accessToken;
            }
        }
    };
});

// Gates NotificationHub.Broadcast — the OpenHub-admin path, not a tenant path. Requires only
// that the caller's issuer is the platform-admin realm, so a human platform-admin user and an
// M2M platform-admin caller are both admitted identically. SendToTenant/SendToUser have no
// policy of their own: any authenticated connection may call them, restricted at runtime by
// MessageTargetAuthorizationEngine's own-realm-or-platform-admin check.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RealtimeMessagingBroadcastPublisher", policy => policy.RequireAssertion(ctx => ctx.User.IsPlatformAdmin()));

// ── iDesign layer registrations ───────────────────────────────────────────────
// Each layer owns its own DI setup via a static Add<Layer>() extension method, so the layer
// can be extracted to its own DLL without touching Program.cs.
builder.Services.AddAccessors();
builder.Services.AddEngines();
builder.Services.AddManagers();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapHub<NotificationHub>(ApiConst.HubRoute);

app.Run();

// Exposes the top-level-statement-generated Program class to WebApplicationFactory<Program> in
// the test project (which otherwise can't see it — top-level statements generate it `internal`).
public partial class Program;
