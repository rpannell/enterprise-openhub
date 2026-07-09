using OpenHub.SalesOrg.Api.Mappers;

namespace OpenHub.SalesOrg.Api.Mappers.Setup;

public static class MapperServiceExtensions
{
    public static IServiceCollection AddMappers(this IServiceCollection services)
    {
        services.AddSingleton<TenantMapper>();
        services.AddSingleton<SalesPersonMapper>();
        services.AddSingleton<SalesRoleMapper>();
        services.AddSingleton<SalesOrganizationUnitMapper>();
        services.AddSingleton<SalesAssignmentMapper>();

        return services;
    }
}
