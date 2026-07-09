# OpenHub.Platform.ServiceDefaults

Shared cross-cutting configuration for every API and background job in the platform:
OpenTelemetry, health checks, service discovery, CORS, correlation IDs, and the
`AddConsulConfiguration`/`AddVaultSecrets` helpers. Referenced by every service project; call
`builder.AddServiceDefaults()` once from `Program.cs`.

## OpenTelemetry

`ConfigureOpenTelemetry()` (called by `AddServiceDefaults()`) wires up, for every service:

- **Tracing**: ASP.NET Core, HttpClient, EF Core, and Redis (StackExchange.Redis) instrumentation,
  plus a named `ActivitySource` (the app's own name) for custom spans.
- **Metrics**: ASP.NET Core, HttpClient, and .NET runtime instrumentation, plus the `Npgsql`
  meter. Always exported via a Prometheus scrape endpoint (`/metrics`).
- **Logging**: structured OpenTelemetry logging with formatted messages and scopes included.

### OTLP export

If an OTLP collector endpoint is configured, traces/metrics/logs are also pushed there (in
addition to the `/metrics` Prometheus scrape). The endpoint is resolved in this order:

1. `OTEL_EXPORTER_OTLP_ENDPOINT` — set automatically by Aspire's dev orchestration, pointing at
   the Aspire dashboard's own collector. Takes priority so local dev telemetry keeps showing up
   there.
2. `Otel:CollectorEndpoint` — read from configuration (Consul's `platform/config` KV path in
   this platform), for standalone/production runs where Aspire isn't orchestrating. See
   `src/Platform/OpenHub.Platform.AppHost/Consul/platform-config.json` for the dev seed value.

If neither is set, OTLP export is skipped and only the Prometheus `/metrics` endpoint is
available — matches this platform's existing "skip silently when infra isn't configured"
convention (same as `AddConsulConfiguration`/`AddVaultSecrets`).

**Ordering requirement**: `AddConsulConfiguration()`/`AddVaultSecrets()` must be called *before*
`AddServiceDefaults()` in `Program.cs`. `builder.Configuration` is a live `ConfigurationManager`,
so the eager `Otel:CollectorEndpoint` read inside `AddServiceDefaults()` only sees configuration
sources registered before that call — Consul's KV values won't be visible if added afterward.

### Coverage

Every API and long-running background job (`CRM.Api`, `SalesOrg.Api`, `ProductManager.Api`,
`Administration.Api`, `CRM.EventProcessor`, `SalesOrg.EventProcessor`,
`ProductManager.ElasticProcessor`, `ProductManager.IdentityEventProcessor`,
`ProductManager.ReindexJob`) references this project and calls `AddServiceDefaults()`, so all of
them get this instrumentation identically — there's no separate "background job telemetry"
project, `ConfigureOpenTelemetry()` is generic over `IHostApplicationBuilder`, which both
`WebApplicationBuilder` (APIs) and `Host.CreateApplicationBuilder` (console-hosted jobs) satisfy.

**Deliberately excluded**: `OpenHub.ProductManager.SeedTool` does not reference this project and
has no OpenTelemetry — it's a one-shot, manually-invoked CLI dev/load-testing tool, not a running
service. See its own README for the reasoning.

## Configuration reference

| Key | What it does | Default |
|---|---|---|
| `Otel:CollectorEndpoint` | OTLP gRPC endpoint (traces/metrics/logs) used when `OTEL_EXPORTER_OTLP_ENDPOINT` isn't set by Aspire. | `http://otel-collector:4317` (dev seed value — no `otel-collector` container is provisioned in `AppHost.cs` yet; this is a placeholder for production/standalone deployments that run one). |
| `Consul:Url` | Consul agent address for `AddConsulConfiguration()`. | unset (skipped) |
| `Vault:Address` / `Vault:Token` | Vault address/token for `AddVaultSecrets()`. | unset (skipped) |
