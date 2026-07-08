using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Internal;
using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak.Extensions;

public static class KeyCloakAccessorExtensions
{
    public static IServiceCollection AddKeyCloakAccessor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KeyCloakOptions>(configuration.GetSection(KeyCloakOptions.SectionName));

        // Singleton so the cached admin token survives across accessor instances —
        // each typed client below is resolved fresh per injection, but they all share
        // this one token cache/semaphore.
        services.AddSingleton<KeyCloakAdminTokenProvider>();

        // BaseAddress resolves in priority order:
        //   1. KeyCloak:BaseUrl (explicit config or Vault secret)
        //   2. ConnectionStrings:KeyCloak (injected by Aspire WithReference(keycloak))
        // Aspire's AddServiceDiscovery() (registered by AddServiceDefaults()) handles
        // resolving logical names like "http://keycloak" to the real container address.
        // Register on the interface so DI always routes through IHttpClientFactory.
        // If AddHttpClient<Impl>() and AddSingleton<IFoo, Impl>() are used separately,
        // the singleton bypasses the typed-client factory and gets a bare HttpClient
        // with no BaseAddress configured.
        services.AddHttpClient<IKeyCloakRealmAccessor, KeyCloakRealmAccessor>()
            .ConfigureHttpClient(ConfigureKeyCloakBaseAddress);
        services.AddHttpClient<IKeyCloakOrganizationAccessor, KeyCloakOrganizationAccessor>()
            .ConfigureHttpClient(ConfigureKeyCloakBaseAddress);
        services.AddHttpClient<IKeyCloakUserAccessor, KeyCloakUserAccessor>()
            .ConfigureHttpClient(ConfigureKeyCloakBaseAddress);
        return services;
    }

    private static void ConfigureKeyCloakBaseAddress(IServiceProvider sp, HttpClient client)
    {
        var opts = sp.GetRequiredService<IOptions<KeyCloakOptions>>().Value;
        var cfg = sp.GetRequiredService<IConfiguration>();

        var baseUrl = !string.IsNullOrWhiteSpace(opts.BaseUrl)
            ? opts.BaseUrl
            : cfg.GetConnectionString("KeyCloak");

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "Keycloak base URL not configured. Set KeyCloak:BaseUrl or ConnectionStrings:KeyCloak.");

        client.BaseAddress = new Uri(baseUrl);
    }

    /// <summary>
    /// Registers <see cref="IKeyCloakM2MTokenAccessor"/> for machine-to-machine
    /// client_credentials tokens, one client secret per tenant realm (see
    /// <see cref="M2MOptions"/>). Opt-in and separate from <see cref="AddKeyCloakAccessor"/>
    /// so services that don't need M2M auth (e.g. ProductManager.Api) aren't forced
    /// to carry Vault-backed M2M secret config.
    /// </summary>
    public static IServiceCollection AddKeyCloakM2MTokenAccessor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KeyCloakOptions>(configuration.GetSection(KeyCloakOptions.SectionName));
        services.Configure<M2MOptions>(configuration.GetSection(M2MOptions.SectionName));
        services.AddHybridCache();

        services.AddHttpClient<IKeyCloakM2MTokenAccessor, KeyCloakM2MTokenAccessor>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<KeyCloakOptions>>().Value;
                var cfg  = sp.GetRequiredService<IConfiguration>();

                var baseUrl = !string.IsNullOrWhiteSpace(opts.BaseUrl)
                    ? opts.BaseUrl
                    : cfg.GetConnectionString("KeyCloak");

                if (string.IsNullOrWhiteSpace(baseUrl))
                    throw new InvalidOperationException(
                        "Keycloak base URL not configured. Set KeyCloak:BaseUrl or ConnectionStrings:KeyCloak.");

                client.BaseAddress = new Uri(baseUrl);
            });
        return services;
    }
}
