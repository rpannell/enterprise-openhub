using OpenHub.RealtimeMessaging.Api.Accessors.Interfaces;

namespace OpenHub.RealtimeMessaging.Api.Accessors.Setup;

public static class AccessorServiceExtensions
{
    public static IServiceCollection AddAccessors(this IServiceCollection services)
    {
        services.AddScoped<INotificationHubAccessor, NotificationHubAccessor>();

        return services;
    }
}
