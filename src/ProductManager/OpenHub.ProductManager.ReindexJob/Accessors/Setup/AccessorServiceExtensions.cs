using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using OpenHub.Accessor.ElasticIndex.Extensions;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ReindexJob.Accessors.Interfaces;

namespace OpenHub.ProductManager.ReindexJob.Accessors.Setup;

public static class AccessorServiceExtensions
{
    public static IServiceCollection AddAccessors(this IServiceCollection services, IConfiguration configuration)
    {
        var httpClientBuilder = services.AddHttpClient<IProductManagerApiAccessor, ProductManagerApiAccessor>(client =>
        {
            // Same Aspire service-discovery convention as ElasticProcessor's accessor —
            // "product-manager-api" is the Aspire resource name, resolved automatically
            // when this project's resource has .WithReference(productManagerApi).
            var baseUrl = configuration["ProductManagerApi:BaseUrl"] ?? "https+http://product-manager-api";
            client.BaseAddress = new Uri(baseUrl);
        });

        // Overrides (not duplicates — same named pipeline) AddServiceDefaults' global standard
        // resilience handler's 10s attempt timeout, which is too tight for this specific client:
        // a page fetch can queue behind Postgres contention (e.g. a concurrent bulk seed/COPY)
        // long enough to exceed it even though the request is still healthy, just slow.
        // TotalRequestTimeout must comfortably exceed 3 attempts at 30s plus retry backoff.
        services.Configure<HttpStandardResilienceOptions>(httpClientBuilder.Name, options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
        });

        // Same shared ProductDocument type ElasticProcessor writes with — see
        // OpenHub.Accessor.ElasticIndex and OpenHub.ProductManager.Contracts.ProductDocument.
        services.AddElasticIndexAccessor<ProductDocument>();

        return services;
    }
}
