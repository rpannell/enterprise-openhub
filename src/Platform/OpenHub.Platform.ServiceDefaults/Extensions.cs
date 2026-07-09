using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Configuration;
using Scalar.AspNetCore;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using Winton.Extensions.Configuration.Consul;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string MetricsEndpointPath = "/metrics";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        builder.AddOpenHubCors();

        // Registers correlation ID middleware via IStartupFilter so it runs first in the
        // pipeline automatically — no explicit app.Use* call needed in each service.
        builder.Services.AddTransient<IStartupFilter, CorrelationIdStartupFilter>();

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Npgsql")
                    .AddPrometheusExporter();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health/liveness/metrics requests from tracing — they generate noise
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                            && !context.Request.Path.StartsWithSegments(MetricsEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    // Resolves IConnectionMultiplexer from DI lazily (deferred until the
                    // TracerProvider is actually built) — works for every service that calls
                    // AddRedisDistributedCache("Redis"), no explicit connection wiring needed here.
                    .AddRedisInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Aspire's dev orchestration injects OTEL_EXPORTER_OTLP_ENDPOINT automatically, pointing
        // at the Aspire dashboard's own collector — prefer that when present so local dev
        // telemetry keeps showing up there. Otherwise fall back to the platform-wide collector
        // endpoint from Consul (Otel:CollectorEndpoint under platform/config), for
        // standalone/production runs where Aspire isn't orchestrating.
        //
        // Reading Otel:CollectorEndpoint here requires AddConsulConfiguration()/AddVaultSecrets()
        // to have already run before AddServiceDefaults() in Program.cs — builder.Configuration
        // is a live ConfigurationManager, so sources added afterward aren't visible to this eager
        // read yet.
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? builder.Configuration["Otel:CollectorEndpoint"];

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            var endpointUri = new Uri(otlpEndpoint);
            // gRPC (port 4317 by convention) matches the collector endpoint shape used platform-wide.
            builder.Services.AddOpenTelemetry().UseOtlpExporter(OtlpExportProtocol.Grpc, endpointUri);

            // UseOtlpExporter() above covers traces/metrics/logs uniformly. Metrics additionally
            // keep the existing Prometheus scrape exporter (see WithMetrics above) — both push
            // (OTLP) and pull (/metrics) paths stay available side by side.
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Registers a CORS policy driven by <c>Cors:AllowedOrigins</c> in configuration.
    /// In development, falls back to allowing any origin when no origins are configured.
    /// Call <c>app.UseCors()</c> in the middleware pipeline to apply it.
    /// </summary>
    public static TBuilder AddOpenHubCors<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var origins = builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>();

                if (origins is { Length: > 0 })
                    policy.WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                else if (builder.Environment.IsDevelopment())
                    // Dev fallback: allow any origin so local front-ends connect without config
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        return builder;
    }

    /// <summary>
    /// Adds correlation ID middleware. Reads <c>X-Correlation-Id</c> from the incoming request
    /// (or generates a new UUID), echoes it in the response header, tags the active OTel span,
    /// and injects it into the log scope so every log entry carries the ID.
    /// Access the value anywhere via <c>HttpContext.Items["CorrelationId"]</c>.
    /// </summary>
    public static WebApplication UseCorrelationId(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health and metrics endpoints are always exposed so Kubernetes/Aspire probes work
        // across all environments. Restrict access at the network/ingress layer in production.
        app.MapHealthChecks(HealthEndpointPath);

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        // Prometheus scrape endpoint — consumed by Prometheus or Aspire dashboard
        app.MapPrometheusScrapingEndpoint(MetricsEndpointPath);

        return app;
    }

    /// <summary>
    /// Loads configuration from one or more Consul KV paths, in order (later paths override earlier ones).
    /// Silently skips if <c>Consul:Url</c> is not set — services start normally without Consul in dev.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="kvPaths">
    /// KV paths to load, from most-general to most-specific.
    /// Example: <c>"platform/config"</c> (system-wide) then <c>"product-manager/config"</c> (service-specific).
    /// </param>
    public static TBuilder AddConsulConfiguration<TBuilder>(
        this TBuilder builder,
        params string[] kvPaths)
        where TBuilder : IHostApplicationBuilder
    {
        var consulUrl = builder.Configuration["Consul:Url"];
        if (string.IsNullOrWhiteSpace(consulUrl))
            return builder;

        foreach (var path in kvPaths)
        {
            builder.Configuration.AddConsul(path, options =>
            {
                options.ConsulConfigurationOptions = cco =>
                    cco.Address = new Uri(consulUrl);
                options.Optional = true;
                options.ReloadOnChange = true;
                options.PollWaitTime = TimeSpan.FromSeconds(5);
            });
        }

        return builder;
    }

    /// <summary>
    /// Loads secrets from one or more Vault KV v2 paths, in order (later paths override earlier ones).
    /// Silently skips if <c>Vault:Address</c> is not set — services start normally without Vault in dev.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="secretPaths">
    /// KV v2 secret paths (without the "secret/data/" prefix), from most-general to most-specific.
    /// Example: <c>"platform"</c> (system-wide) then <c>"product-manager"</c> (service-specific).
    /// </param>
    public static TBuilder AddVaultSecrets<TBuilder>(
        this TBuilder builder,
        params string[] secretPaths)
        where TBuilder : IHostApplicationBuilder
    {
        var vaultAddress = builder.Configuration["Vault:Address"];
        var vaultToken   = builder.Configuration["Vault:Token"];

        if (string.IsNullOrWhiteSpace(vaultAddress))
            return builder;

        try
        {
            var client = new VaultClient(new VaultClientSettings(
                vaultAddress,
                new TokenAuthMethodInfo(vaultToken ?? string.Empty)));

            var combined = new Dictionary<string, string?>();

            foreach (var path in secretPaths)
            {
                try
                {
                    var secret = client.V1.Secrets.KeyValue.V2
                        .ReadSecretAsync(path, mountPoint: "secret")
                        .GetAwaiter().GetResult();

                    if (secret?.Data?.Data is { } data)
                    {
                        foreach (var kv in data)
                            combined[kv.Key] = kv.Value?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    // Individual path not found — log and continue
                    Console.WriteLine($"[Vault] Skipping '{path}': {ex.Message}");
                }
            }

            if (combined.Count > 0)
                builder.Configuration.AddInMemoryCollection(combined);
        }
        catch (Exception ex)
        {
            // Vault unreachable — non-fatal, app starts without those secrets
            Console.WriteLine($"[Vault] Could not connect to {vaultAddress}: {ex.Message}");
        }

        return builder;
    }

    /// <summary>
    /// Maps OpenAPI spec generation and the OpenHub-branded Scalar documentation UI.
    /// Call once per API in Program.cs after <c>app.Build()</c>:
    /// <code>app.MapOpenHubApiDocs("OpenHub Product Manager API v1");</code>
    /// Scalar is available in development by default. Pass <c>enableInProduction: true</c>
    /// to expose it in other environments (e.g. behind an auth middleware or IP allowlist).
    /// </summary>
    public static WebApplication MapOpenHubApiDocs(
        this WebApplication app,
        string title = "OpenHub API",
        string path = "/api-docs",
        bool enableInProduction = false)
    {
        // Generates /openapi/v1.json — consumed by Scalar below
        app.MapOpenApi();

        if (!enableInProduction && !app.Environment.IsDevelopment())
            return app;

        app.MapScalarApiReference(path, options => options
            .WithTitle(title)
            .WithTheme(ScalarTheme.None)
            .WithCustomCss(OpenHubScalarTheme.Css)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .AddPreferredSecuritySchemes("Bearer"));

        // Redirect root → docs so the Aspire dashboard link opens Scalar directly
        app.MapGet("/", () => Results.Redirect(path, permanent: false))
           .ExcludeFromDescription();

        return app;
    }
}
