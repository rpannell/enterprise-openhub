using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.ProductManager.ReindexJob;
using OpenHub.ProductManager.ReindexJob.Accessors.Setup;
using OpenHub.ProductManager.ReindexJob.Managers.Engines.Setup;
using OpenHub.ProductManager.ReindexJob.Managers.Setup;
using OpenHub.ProductManager.ReindexJob.Options;

var builder = Host.CreateApplicationBuilder(args);

// Must run before AddServiceDefaults() so Otel:CollectorEndpoint is visible to it.
builder.AddConsulConfiguration("platform/config", "product-manager-reindex-job/config");
builder.AddVaultSecrets("product-manager", "product-manager-reindex-job");

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("Redis");

// Bootstrap client only — authenticates via the superuser connection string Aspire
// injects. Consumed internally by ElasticIndexAccessor to bootstrap the shared index,
// mint a scoped writer API key, create versioned indices, and bulk-write documents.
builder.AddElasticsearchClient("Elasticsearch");

builder.Services.Configure<ReindexJobOptions>(builder.Configuration.GetSection(ReindexJobOptions.SectionName));
builder.Services.AddKeyCloakM2MTokenAccessor(builder.Configuration);

builder.Services.AddAccessors(builder.Configuration);
builder.Services.AddEngines();
builder.Services.AddManagers();

builder.Services.AddHostedService<ReindexHostedService>();

await builder.Build().RunAsync();
