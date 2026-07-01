using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.Function.Managers.Engines.Interfaces;

namespace OpenHub.ProductManager.Function.Managers.Engines.Setup;

public static class EngineServiceExtensions
{
    public static IServiceCollection AddEngines(this IServiceCollection services)
    {
        services.AddTransient<IProductSyncEngine, ProductSyncEngine>();
        return services;
    }
}
