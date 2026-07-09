using OpenHub.CRM.Api.Managers;
using OpenHub.CRM.Api.Managers.Interfaces;

namespace OpenHub.CRM.Api.Managers.Setup;

public static class ManagerServiceExtensions
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddScoped<IClientManager, ClientManager>();

        return services;
    }
}
