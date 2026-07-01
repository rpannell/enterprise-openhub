using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.Function.Managers.Interfaces;

namespace OpenHub.ProductManager.Function.Managers.Setup;

public static class ManagerServiceExtensions
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddScoped<IProductSyncManager, ProductSyncManager>();
        services.AddScoped<INightlyIndexManager, NightlyIndexManager>();
        return services;
    }
}
