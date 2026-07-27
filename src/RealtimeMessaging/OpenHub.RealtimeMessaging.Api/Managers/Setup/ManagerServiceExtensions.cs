using OpenHub.RealtimeMessaging.Api.Managers.Interfaces;

namespace OpenHub.RealtimeMessaging.Api.Managers.Setup;

public static class ManagerServiceExtensions
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddScoped<IMessageDispatchManager, MessageDispatchManager>();

        return services;
    }
}
