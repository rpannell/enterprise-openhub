using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenHub.Accessor.RabbitMq;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RabbitMqMessagePublisher"/> as a singleton
    /// <see cref="IMessagePublisher"/> and as a hosted service so the
    /// connection opens, the durable fanout exchange is declared, and the
    /// channel pool is created automatically with the host.
    ///
    /// Usage:
    ///   builder.Services.AddRabbitMqPublisher(builder.Configuration.GetSection("RabbitMq"));
    /// </summary>
    public static IServiceCollection AddRabbitMqPublisher(
        this IServiceCollection services,
        IConfiguration configurationSection)
    {
        services.Configure<RabbitMqPublisherOptions>(configurationSection);
        return services.AddRabbitMqPublisherCore();
    }

    /// <summary>
    /// Overload for configuring options in code instead of from configuration.
    /// </summary>
    public static IServiceCollection AddRabbitMqPublisher(
        this IServiceCollection services,
        Action<RabbitMqPublisherOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services.AddRabbitMqPublisherCore();
    }

    private static IServiceCollection AddRabbitMqPublisherCore(this IServiceCollection services)
    {
        // Single instance shared as both the IMessagePublisher API surface
        // and the IHostedService that manages its lifecycle.
        services.AddSingleton<RabbitMqMessagePublisher>();
        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RabbitMqMessagePublisher>());
        services.AddHostedService(sp => sp.GetRequiredService<RabbitMqMessagePublisher>());

        return services;
    }
}
