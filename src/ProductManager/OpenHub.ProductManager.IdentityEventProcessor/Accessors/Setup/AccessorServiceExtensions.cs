using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.IdentityEventProcessor.Accessors.Interfaces;

namespace OpenHub.ProductManager.IdentityEventProcessor.Accessors.Setup;

public static class AccessorServiceExtensions
{
    public static IServiceCollection AddAccessors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IProductManagerApiAccessor, ProductManagerApiAccessor>(client =>
        {
            // Same Aspire service-discovery convention as ElasticProcessor/ReindexJob's
            // ProductManagerApiAccessor — see those for the full rationale.
            var baseUrl = configuration["ProductManagerApi:BaseUrl"] ?? "https+http://product-manager-api";
            client.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }
}
