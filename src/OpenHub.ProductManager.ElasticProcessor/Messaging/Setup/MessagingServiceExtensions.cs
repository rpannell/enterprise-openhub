using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Retry.Simple;
using OpenHub.ProductManager.ElasticProcessor.Messaging.Handlers;
using OpenHub.ProductManager.ElasticProcessor.Messaging.Serialization;

namespace OpenHub.ProductManager.ElasticProcessor.Messaging.Setup;

public static class MessagingServiceExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));

        // Must be registered before AddRebus below — the generic host starts hosted
        // services in registration order, so this runs (and finishes) before Rebus's
        // own hosted service starts declaring/consuming the queue.
        services.AddHostedService<RabbitMqTopologyInitializer>();

        services.AddRebus((configure, provider) =>
        {
            var messaging = provider.GetRequiredService<IOptions<MessagingOptions>>().Value;

            return configure
                .Logging(l => l.MicrosoftExtensionsLogging(provider.GetRequiredService<ILoggerFactory>()))
                .Transport(t => t.UseRabbitMq(GetConnectionString(configuration), messaging.QueueName))
                .Options(o =>
                {
                    // First-level (immediate) retries only, no delayed second-level retries —
                    // host.json had zero retry config previously, so this preserves that
                    // low-friction default while adding basic resilience Rebus gives for free.
                    o.RetryStrategy(errorQueueName: messaging.ErrorQueueName, maxDeliveryAttempts: 5);

                    // Bridges the publisher's headerless messages onto Rebus's stock JSON
                    // deserializer — see StampMessageTypeHeaderStep for the full rationale.
                    o.Decorate<IPipeline>(c =>
                    {
                        var pipeline = c.Get<IPipeline>();
                        return new PipelineStepInjector(pipeline)
                            .OnReceive(new StampMessageTypeHeaderStep(), PipelineRelativePosition.Before, typeof(DeserializeIncomingMessageStep));
                    });
                });
        });

        services.AutoRegisterHandlersFromAssemblyOf<ProductChangedEventHandler>();

        return services;
    }

    internal static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException(
                "RabbitMQ connection string not found. Expected Aspire to inject " +
                "ConnectionStrings:RabbitMq via .WithReference(rabbitmq) in AppHost.cs.");
}
