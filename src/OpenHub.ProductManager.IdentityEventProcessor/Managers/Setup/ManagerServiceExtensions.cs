using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.IdentityEventProcessor.Managers.Interfaces;

namespace OpenHub.ProductManager.IdentityEventProcessor.Managers.Setup;

public static class ManagerServiceExtensions
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddScoped<ITenantSyncManager, TenantSyncManager>();
        services.AddScoped<IUserSyncManager, UserSyncManager>();

        return services;
    }
}
