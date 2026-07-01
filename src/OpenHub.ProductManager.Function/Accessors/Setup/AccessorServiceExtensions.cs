using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.Function.Accessors.Interfaces;

namespace OpenHub.ProductManager.Function.Accessors.Setup;

public static class AccessorServiceExtensions
{
    public static IServiceCollection AddAccessors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IProductManagerApiAccessor, ProductManagerApiAccessor>(client =>
        {
            // "product-manager-api" is the Aspire resource name (AppHost.cs); with
            // .WithReference(productManagerApi) on this Function's resource, Aspire's
            // service discovery (registered for every typed HttpClient by
            // AddServiceDefaults()'s ConfigureHttpClientDefaults) resolves this logical
            // name to the real dev endpoint automatically — no per-service hardcoded
            // URL to keep in sync. ProductManagerApi:BaseUrl overrides it for
            // standalone (non-Aspire) runs.
            var baseUrl = configuration["ProductManagerApi:BaseUrl"] ?? "http://product-manager-api";
            client.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }
}
