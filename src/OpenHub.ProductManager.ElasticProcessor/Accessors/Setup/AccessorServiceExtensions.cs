using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.ElasticProcessor.Accessors.Interfaces;

namespace OpenHub.ProductManager.ElasticProcessor.Accessors.Setup;

public static class AccessorServiceExtensions
{
    public static IServiceCollection AddAccessors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IProductManagerApiAccessor, ProductManagerApiAccessor>(client =>
        {
            // "product-manager-api" is the Aspire resource name (AppHost.cs); with
            // .WithReference(productManagerApi) on this project's resource, Aspire's
            // service discovery (registered for every typed HttpClient by
            // AddServiceDefaults()'s ConfigureHttpClientDefaults) resolves this logical
            // name to the real dev endpoint automatically — no per-service hardcoded
            // URL to keep in sync. ProductManagerApi:BaseUrl overrides it for
            // standalone (non-Aspire) runs.
            //
            // "https+http://" is Aspire's documented scheme-priority syntax: it tries
            // an https endpoint first, falling back to http. Dev today only registers
            // an http endpoint for product-manager-api (Services:product-manager-api:http:0),
            // so this resolves via the http fallback there, but stays correct without
            // changes if an https endpoint is added later (e.g. in a deployed environment).
            var baseUrl = configuration["ProductManagerApi:BaseUrl"] ?? "https+http://product-manager-api";
            client.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }
}
