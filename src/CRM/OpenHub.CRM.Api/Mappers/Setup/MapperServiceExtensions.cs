using OpenHub.CRM.Api.Mappers;

namespace OpenHub.CRM.Api.Mappers.Setup;

public static class MapperServiceExtensions
{
    public static IServiceCollection AddMappers(this IServiceCollection services)
    {
        services.AddSingleton<TenantMapper>();
        services.AddSingleton<ClientMapper>();

        return services;
    }
}
