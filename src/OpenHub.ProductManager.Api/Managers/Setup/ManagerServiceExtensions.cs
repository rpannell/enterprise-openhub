using OpenHub.ProductManager.Api.Managers;
using OpenHub.ProductManager.Api.Managers.Interfaces;

namespace OpenHub.ProductManager.Api.Managers.Setup;

public static class ManagerServiceExtensions
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddScoped<IProductManager, ProductManager>();
        services.AddScoped<ILookupManager, LookupManager>();

        return services;
    }
}
