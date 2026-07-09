using OpenHub.CRM.Api.Accessors;
using OpenHub.CRM.Api.Accessors.Interfaces;

namespace OpenHub.CRM.Api.Accessors.Setup;

public static class AccessorServiceExtensions
{
    public static IServiceCollection AddAccessors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantAccessor, TenantAccessor>();
        services.AddScoped<IClientAccessor, ClientAccessor>();

        services.AddHttpClient<ISalesOrgApiAccessor, SalesOrgApiAccessor>(client =>
        {
            // "salesorg-api" is the Aspire resource name (AppHost.cs); with .WithReference(salesOrgApi)
            // on crm-api's resource, Aspire's service discovery (registered for every typed
            // HttpClient by AddServiceDefaults()'s ConfigureHttpClientDefaults) resolves this
            // logical name to the real dev endpoint automatically — no per-service hardcoded URL
            // to keep in sync. SalesOrgApi:BaseUrl overrides it for standalone (non-Aspire) runs.
            var baseUrl = configuration["SalesOrgApi:BaseUrl"] ?? "https+http://salesorg-api";
            client.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }
}
