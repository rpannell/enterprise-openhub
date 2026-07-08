using OpenHub.Administration.Api.Managers.Interfaces;

namespace OpenHub.Administration.Api.Managers.Setup;

public static class ManagerServiceExtensions
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationManager, OrganizationManager>();
        services.AddScoped<IUserManager, UserManager>();

        return services;
    }
}
