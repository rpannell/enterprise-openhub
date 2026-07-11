using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenHub.Accessor.RealtimeMessaging.Interfaces;

namespace OpenHub.Accessor.RealtimeMessaging.Setup;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// BaseAddress resolves in priority order:
    ///   1. RealtimeMessagingApi:BaseUrl (explicit config or Vault secret)
    ///   2. "https+http://realtime-messaging-api" — Aspire service discovery logical name,
    ///      resolved by AddServiceDiscovery() (registered by AddServiceDefaults()).
    /// Register on the interface so DI always routes through IHttpClientFactory — see the
    /// same warning in OpenHub.Accessor.KeyCloak's KeyCloakAccessorExtensions.
    /// </summary>
    public static IServiceCollection AddRealtimeMessagingAccessor(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IRealtimeMessagingAccessor, RealtimeMessagingAccessor>()
            .ConfigureHttpClient(client =>
            {
                var baseUrl = configuration["RealtimeMessagingApi:BaseUrl"] ?? "https+http://realtime-messaging-api";
                client.BaseAddress = new Uri(baseUrl);
            });

        return services;
    }
}
