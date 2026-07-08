using OpenHub.ProductManager.Api.Mappers;

namespace OpenHub.ProductManager.Api.Mappers.Setup;

public static class MapperServiceExtensions
{
    public static IServiceCollection AddMappers(this IServiceCollection services)
    {
        services.AddSingleton<ProductMapper>();
        services.AddSingleton<TenantMapper>();
        services.AddSingleton<LookupMapper>();

        return services;
    }
}
