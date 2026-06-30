using OpenHub.ProductManager.Api.Managers.Engines;
using OpenHub.ProductManager.Api.Managers.Engines.Interfaces;

namespace OpenHub.ProductManager.Api.Managers.Engines.Setup;

public static class EngineServiceExtensions
{
    public static IServiceCollection AddEngines(this IServiceCollection services)
    {
        services.AddTransient<IProductEngine, ProductEngine>();
        services.AddTransient<IProductPricingEngine, ProductPricingEngine>();

        return services;
    }
}
