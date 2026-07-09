using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.ProductManager.ElasticProcessor.Accessors.Setup;
using OpenHub.ProductManager.ElasticProcessor.Managers.Engines.Setup;
using OpenHub.ProductManager.ElasticProcessor.Managers.Setup;
using OpenHub.ProductManager.ElasticProcessor.Messaging.Setup;

var builder = Host.CreateApplicationBuilder(args);

// Identity of the M2M-token-consuming component is intentionally NOT renamed to match
// this project — these literal path strings match the already-provisioned Keycloak
// client and Vault secret. See README.md.
// Must run before AddServiceDefaults() so Otel:CollectorEndpoint is visible to it.
builder.AddConsulConfiguration("platform/config", "product-manager-function/config");
builder.AddVaultSecrets("platform", "product-manager-function");

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("Redis");

// Bootstrap client only — authenticates via the superuser connection string Aspire
// injects. Consumed internally by ProductIndexAccessor to create the index and mint
// a scoped writer API key once; nothing else should depend on this client directly.
builder.AddElasticsearchClient("Elasticsearch");

builder.Services.AddKeyCloakM2MTokenAccessor(builder.Configuration);

builder.Services.AddAccessors(builder.Configuration);
builder.Services.AddEngines();
builder.Services.AddManagers();
builder.Services.AddMessaging(builder.Configuration);

builder.Build().Run();
