using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.ElasticProcessor.Managers.Engines.Interfaces;

namespace OpenHub.ProductManager.ElasticProcessor.Managers.Engines.Setup;

public static class EngineServiceExtensions
{
    public static IServiceCollection AddEngines(this IServiceCollection services)
    {
        services.AddTransient<IProductSyncEngine, ProductSyncEngine>();
        return services;
    }
}
