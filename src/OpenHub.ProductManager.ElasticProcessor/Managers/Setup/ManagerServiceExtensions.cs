using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.ElasticProcessor.Managers.Interfaces;

namespace OpenHub.ProductManager.ElasticProcessor.Managers.Setup;

public static class ManagerServiceExtensions
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddScoped<IProductSyncManager, ProductSyncManager>();
        return services;
    }
}
