using OpenHub.RealtimeMessaging.Api.Managers.Engines.Interfaces;

namespace OpenHub.RealtimeMessaging.Api.Managers.Engines.Setup;

public static class EngineServiceExtensions
{
    public static IServiceCollection AddEngines(this IServiceCollection services)
    {
        services.AddSingleton<IMessageTargetAuthorizationEngine, MessageTargetAuthorizationEngine>();

        return services;
    }
}
