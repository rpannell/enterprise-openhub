using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.KeyCloak.Interfaces;

namespace OpenHub.Accessor.KeyCloak.Extensions;

public static class KeyCloakAccessorExtensions
{
    public static IServiceCollection AddKeyCloakAccessor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KeyCloakOptions>(configuration.GetSection(KeyCloakOptions.SectionName));

        // BaseAddress is set from config so Aspire service discovery can resolve "http://keycloak"
        // to the real container URL. The global AddServiceDiscovery() registered by AddServiceDefaults()
        // handles the resolution automatically for all typed HttpClients.
        services.AddHttpClient<KeyCloakRealmAccessor>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<KeyCloakOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
            });

        services.AddSingleton<IKeyCloakRealmAccessor, KeyCloakRealmAccessor>();
        return services;
    }
}
