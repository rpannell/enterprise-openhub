using Microsoft.Extensions.DependencyInjection;
using OpenHub.Accessor.RealtimeMessaging.Interfaces;

namespace OpenHub.Accessor.RealtimeMessaging.Setup;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// No <c>HttpClient</c> involved — <see cref="RealtimeMessagingAccessor"/> talks to
    /// OpenHub.RealtimeMessaging.Api's hub directly via <c>Microsoft.AspNetCore.SignalR.Client</c>,
    /// connecting fresh per call. Hub URL resolves from <c>RealtimeMessagingApi:HubUrl</c> config,
    /// falling back to Aspire's service-discovery logical name.
    /// </summary>
    public static IServiceCollection AddRealtimeMessagingAccessor(this IServiceCollection services)
    {
        services.AddSingleton<IRealtimeMessagingAccessor, RealtimeMessagingAccessor>();

        return services;
    }
}
