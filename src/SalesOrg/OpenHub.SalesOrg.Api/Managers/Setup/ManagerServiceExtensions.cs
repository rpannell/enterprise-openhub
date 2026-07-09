using OpenHub.SalesOrg.Api.Managers;
using OpenHub.SalesOrg.Api.Managers.Interfaces;

namespace OpenHub.SalesOrg.Api.Managers.Setup;

public static class ManagerServiceExtensions
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddScoped<ISalesPersonManager, SalesPersonManager>();
        services.AddScoped<ISalesRoleManager, SalesRoleManager>();
        services.AddScoped<ISalesOrganizationUnitManager, SalesOrganizationUnitManager>();
        services.AddScoped<ISalesAssignmentManager, SalesAssignmentManager>();

        return services;
    }
}
