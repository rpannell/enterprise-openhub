using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenHub.Accessor.KeyCloak.Extensions;
using OpenHub.ProductManager.Function.Accessors.Setup;
using OpenHub.ProductManager.Function.Managers.Engines.Setup;
using OpenHub.ProductManager.Function.Managers.Setup;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddConsulConfiguration("platform/config", "product-manager-function/config");
builder.AddVaultSecrets("platform", "product-manager-function");

builder.AddRedisDistributedCache("redis");
builder.Services.AddHybridCache();

// The RabbitMQTrigger's ConnectionStringSetting requires one full AMQP URI app
// setting (no discrete host/user/password settings in v2.x of the extension),
// so compose it from the same discrete config pieces used elsewhere in the repo.
var rmq = builder.Configuration.GetSection("RabbitMq");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["RabbitMQConnection"] = $"amqp://{rmq["UserName"]}:{rmq["Password"]}@{rmq["HostName"]}:{rmq["Port"]}/",
});

builder.Services.AddKeyCloakM2MTokenAccessor(builder.Configuration);

builder.Services.AddAccessors(builder.Configuration);
builder.Services.AddEngines();
builder.Services.AddManagers();

builder.Build().Run();
