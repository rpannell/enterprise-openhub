using OpenHub.Administration.Api.Managers.Engines.Interfaces;

namespace OpenHub.Administration.Api.Managers.Engines.Setup;

public static class EngineServiceExtensions
{
    public static IServiceCollection AddEngines(this IServiceCollection services)
    {
        services.AddSingleton<IRoleAssignmentEngine, RoleAssignmentEngine>();
        return services;
    }
}
