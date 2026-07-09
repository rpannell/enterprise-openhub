using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenHub.CRM.EventProcessor.Messaging.Setup;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddConsulConfiguration("platform/config", "crm-event-processor/config");
builder.AddVaultSecrets("platform", "crm-event-processor");

builder.Services.AddMessaging(builder.Configuration);

builder.Build().Run();
