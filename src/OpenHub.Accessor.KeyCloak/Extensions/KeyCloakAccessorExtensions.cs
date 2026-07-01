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

        // BaseAddress resolves in priority order:
        //   1. KeyCloak:BaseUrl (explicit config or Vault secret)
        //   2. ConnectionStrings:keycloak (injected by Aspire WithReference(keycloak))
        // Aspire's AddServiceDiscovery() (registered by AddServiceDefaults()) handles
        // resolving logical names like "http://keycloak" to the real container address.
        // Register on the interface so DI always routes through IHttpClientFactory.
        // If AddHttpClient<Impl>() and AddSingleton<IFoo, Impl>() are used separately,
        // the singleton bypasses the typed-client factory and gets a bare HttpClient
        // with no BaseAddress configured.
        services.AddHttpClient<IKeyCloakRealmAccessor, KeyCloakRealmAccessor>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<KeyCloakOptions>>().Value;
                var cfg  = sp.GetRequiredService<IConfiguration>();

                var baseUrl = !string.IsNullOrWhiteSpace(opts.BaseUrl)
                    ? opts.BaseUrl
                    : cfg.GetConnectionString("keycloak");

                if (string.IsNullOrWhiteSpace(baseUrl))
                    throw new InvalidOperationException(
                        "Keycloak base URL not configured. Set KeyCloak:BaseUrl or ConnectionStrings:keycloak.");

                client.BaseAddress = new Uri(baseUrl);
            });
        return services;
    }
}
