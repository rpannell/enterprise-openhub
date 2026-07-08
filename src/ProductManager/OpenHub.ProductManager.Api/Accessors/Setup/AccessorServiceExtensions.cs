using OpenHub.ProductManager.Api.Accessors;
using OpenHub.ProductManager.Api.Accessors.Interfaces;

namespace OpenHub.ProductManager.Api.Accessors.Setup;

public static class AccessorServiceExtensions
{
    public static IServiceCollection AddAccessors(this IServiceCollection services)
    {
        services.AddScoped<ITenantAccessor, TenantAccessor>();
        services.AddScoped<ILookupCacheAccessor, LookupCacheAccessor>();
        services.AddScoped<ICatalogLookupAccessor, CatalogLookupAccessor>();
        services.AddScoped<IProductAccessor, ProductAccessor>();
        services.AddScoped<IProductPriceAccessor, ProductPriceAccessor>();

        return services;
    }
}
