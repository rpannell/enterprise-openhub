using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.ReindexJob.Managers.Engines.Interfaces;

namespace OpenHub.ProductManager.ReindexJob.Managers.Engines.Setup;

public static class EngineServiceExtensions
{
    public static IServiceCollection AddEngines(this IServiceCollection services)
    {
        services.AddTransient<IReindexEngine, ReindexEngine>();
        return services;
    }
}
