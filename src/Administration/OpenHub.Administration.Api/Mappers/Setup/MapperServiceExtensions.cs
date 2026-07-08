namespace OpenHub.Administration.Api.Mappers.Setup;

public static class MapperServiceExtensions
{
    public static IServiceCollection AddMappers(this IServiceCollection services)
    {
        services.AddSingleton<OrganizationMapper>();
        services.AddSingleton<UserMapper>();
        services.AddSingleton<RoleMapper>();

        return services;
    }
}
