using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenHub.Accessor.ElasticIndex.Extensions;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ReindexJob.Accessors.Interfaces;

namespace OpenHub.ProductManager.ReindexJob.Accessors.Setup;

public static class AccessorServiceExtensions
{
    public static IServiceCollection AddAccessors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IProductManagerApiAccessor, ProductManagerApiAccessor>(client =>
        {
            // Same Aspire service-discovery convention as ElasticProcessor's accessor —
            // "product-manager-api" is the Aspire resource name, resolved automatically
            // when this project's resource has .WithReference(productManagerApi).
            var baseUrl = configuration["ProductManagerApi:BaseUrl"] ?? "https+http://product-manager-api";
            client.BaseAddress = new Uri(baseUrl);
        });

        // Same shared ProductDocument type ElasticProcessor writes with — see
        // OpenHub.Accessor.ElasticIndex and OpenHub.ProductManager.Contracts.ProductDocument.
        services.AddElasticIndexAccessor<ProductDocument>();

        return services;
    }
}
