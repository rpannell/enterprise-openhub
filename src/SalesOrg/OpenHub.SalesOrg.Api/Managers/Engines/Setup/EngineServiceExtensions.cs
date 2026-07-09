using OpenHub.SalesOrg.Api.Managers.Engines;
using OpenHub.SalesOrg.Api.Managers.Engines.Interfaces;

namespace OpenHub.SalesOrg.Api.Managers.Engines.Setup;

public static class EngineServiceExtensions
{
    public static IServiceCollection AddEngines(this IServiceCollection services)
    {
        services.AddSingleton<ISalesOrgHierarchyEngine, SalesOrgHierarchyEngine>();

        return services;
    }
}
