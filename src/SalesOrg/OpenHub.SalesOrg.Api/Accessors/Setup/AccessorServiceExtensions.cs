using OpenHub.SalesOrg.Api.Accessors;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;

namespace OpenHub.SalesOrg.Api.Accessors.Setup;

public static class AccessorServiceExtensions
{
    public static IServiceCollection AddAccessors(this IServiceCollection services)
    {
        services.AddScoped<ITenantAccessor, TenantAccessor>();
        services.AddScoped<ISalesPersonAccessor, SalesPersonAccessor>();
        services.AddScoped<ISalesRoleAccessor, SalesRoleAccessor>();
        services.AddScoped<ISalesOrganizationUnitAccessor, SalesOrganizationUnitAccessor>();
        services.AddScoped<ISalesAssignmentAccessor, SalesAssignmentAccessor>();

        return services;
    }
}
