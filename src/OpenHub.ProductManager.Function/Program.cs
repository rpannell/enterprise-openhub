using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenHub.ProductManager.Function.Managers.Setup;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Lets NightlyIndexSchedule be overridden without a redeploy. Skipped silently
// if Consul:Url isn't set.
builder.AddConsulConfiguration("platform/config", "product-manager-function/config");

builder.Services.AddManagers();

builder.Build().Run();
