using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenHub.CRM.EventProcessor.Messaging.Setup;

var builder = Host.CreateApplicationBuilder(args);

// Must run before AddServiceDefaults() so Otel:CollectorEndpoint is visible to it.
builder.AddConsulConfiguration("platform/config", "crm-event-processor/config");
builder.AddVaultSecrets("platform", "crm-event-processor");

builder.AddServiceDefaults();

builder.Services.AddMessaging(builder.Configuration);

builder.Build().Run();
