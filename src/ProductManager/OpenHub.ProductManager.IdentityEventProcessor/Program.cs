using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.ProductManager.IdentityEventProcessor.Accessors.Setup;
using OpenHub.ProductManager.IdentityEventProcessor.Managers.Setup;
using OpenHub.ProductManager.IdentityEventProcessor.Messaging.Setup;

var builder = Host.CreateApplicationBuilder(args);

// Must run before AddServiceDefaults() so Otel:CollectorEndpoint is visible to it.
builder.AddConsulConfiguration("platform/config", "product-manager-identity-event-processor/config");
builder.AddVaultSecrets("platform", "product-manager-identity-event-processor");

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("Redis");
builder.Services.AddHybridCache();

// M2M token accessor (platform-admin token to call ProductManager.Api's sync endpoints) and
// realm accessor (canonical tenant display name lookups) — both needed by TenantSyncManager.
builder.Services.AddKeyCloakM2MTokenAccessor(builder.Configuration);
builder.Services.AddKeyCloakAccessor(builder.Configuration);

builder.Services.AddAccessors(builder.Configuration);
builder.Services.AddManagers();
builder.Services.AddMessaging(builder.Configuration);

builder.Build().Run();
