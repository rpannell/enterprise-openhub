using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Must run before AddServiceDefaults() so Otel:CollectorEndpoint is visible to it.
builder.AddConsulConfiguration("platform/config", "salesorg-event-processor/config");
builder.AddVaultSecrets("platform", "salesorg-event-processor");

builder.AddServiceDefaults();

// Scaffold only — no messaging wired up yet. OpenHub.SalesOrg.Contracts.SalesAssignmentChangedEvent
// documents the intended shape for when SalesAssignment create/move/close starts publishing
// (mirrors OpenHub.CRM.EventProcessor's RabbitMq/Rebus wiring once there's an event to consume).
builder.Build().Run();
