using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Retry.Simple;
using OpenHub.CRM.Contracts;
using OpenHub.CRM.EventProcessor.Messaging.Serialization;

namespace OpenHub.CRM.EventProcessor.Messaging.Setup;

public static class MessagingServiceExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));

        // Must be registered before AddRebus below — the generic host starts hosted services in
        // registration order, so this runs (and finishes) before Rebus's own hosted service
        // starts declaring/consuming its queue.
        services.AddHostedService<RabbitMqTopologyInitializer>();

        services.AddRebus((configure, provider) =>
        {
            var messaging = provider.GetRequiredService<IOptions<MessagingOptions>>().Value;

            return configure
                .Logging(l => l.MicrosoftExtensionsLogging(provider.GetRequiredService<ILoggerFactory>()))
                .Transport(t => t.UseRabbitMq(GetConnectionString(configuration), messaging.QueueName))
                .Options(o =>
                {
                    o.RetryStrategy(errorQueueName: messaging.ErrorQueueName, maxDeliveryAttempts: 5);
                    o.Decorate<IPipeline>(c =>
                    {
                        var pipeline = c.Get<IPipeline>();
                        return new PipelineStepInjector(pipeline)
                            .OnReceive(new StampMessageTypeHeaderStep<ClientChangedEvent>(), PipelineRelativePosition.Before, typeof(DeserializeIncomingMessageStep));
                    });
                });
        });

        services.AutoRegisterHandlersFromAssemblyOf<ClientChangedEventHandler>();

        return services;
    }

    internal static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException(
                "RabbitMQ connection string not found. Expected Aspire to inject " +
                "ConnectionStrings:RabbitMq via .WithReference(rabbitmq) in AppHost.cs.");
}
