using Microsoft.Extensions.DependencyInjection;
using OpenHub.ProductManager.ReindexJob.Managers.Interfaces;

namespace OpenHub.ProductManager.ReindexJob.Managers.Setup;

public static class ManagerServiceExtensions
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddScoped<IReindexManager, ReindexManager>();
        return services;
    }
}
