namespace OpenHub.RealtimeMessaging.Api.Mappers.Setup;

public static class MapperServiceExtensions
{
    public static IServiceCollection AddMappers(this IServiceCollection services)
    {
        services.AddSingleton<MessageMapper>();

        return services;
    }
}
