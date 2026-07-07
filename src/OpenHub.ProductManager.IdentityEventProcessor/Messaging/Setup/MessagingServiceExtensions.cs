using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Retry.Simple;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Messaging.Handlers;
using OpenHub.ProductManager.IdentityEventProcessor.Messaging.Serialization;

namespace OpenHub.ProductManager.IdentityEventProcessor.Messaging.Setup;

public static class MessagingServiceExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));

        // Must be registered before the AddRebus calls below — the generic host starts hosted
        // services in registration order, so this runs (and finishes) before either Rebus bus's
        // own hosted service starts declaring/consuming its queue.
        services.AddHostedService<RabbitMqTopologyInitializer>();

        // Two independent Rebus buses in one process — one input queue each, bound to a
        // different fanout exchange. Rebus.ServiceProvider's keyed AddRebus (key: "tenant" /
        // "user") registers each with IBusRegistry; neither is consulted directly here since
        // this worker only ever handles inbound messages, but the key still scopes each bus's
        // own transport/options/error-queue configuration independently.
        services.AddRebus((configure, provider) =>
        {
            var messaging = provider.GetRequiredService<IOptions<MessagingOptions>>().Value;

            return configure
                .Logging(l => l.MicrosoftExtensionsLogging(provider.GetRequiredService<ILoggerFactory>()))
                .Transport(t => t.UseRabbitMq(GetConnectionString(configuration), messaging.TenantQueueName))
                .Options(o =>
                {
                    o.RetryStrategy(errorQueueName: messaging.TenantErrorQueueName, maxDeliveryAttempts: 5);
                    o.Decorate<IPipeline>(c =>
                    {
                        var pipeline = c.Get<IPipeline>();
                        return new PipelineStepInjector(pipeline)
                            .OnReceive(new StampMessageTypeHeaderStep<TenantChangedEvent>(), PipelineRelativePosition.Before, typeof(DeserializeIncomingMessageStep));
                    });
                });
        }, key: "tenant", isDefaultBus: true);

        services.AddRebus((configure, provider) =>
        {
            var messaging = provider.GetRequiredService<IOptions<MessagingOptions>>().Value;

            return configure
                .Logging(l => l.MicrosoftExtensionsLogging(provider.GetRequiredService<ILoggerFactory>()))
                .Transport(t => t.UseRabbitMq(GetConnectionString(configuration), messaging.UserQueueName))
                .Options(o =>
                {
                    o.RetryStrategy(errorQueueName: messaging.UserErrorQueueName, maxDeliveryAttempts: 5);
                    o.Decorate<IPipeline>(c =>
                    {
                        var pipeline = c.Get<IPipeline>();
                        return new PipelineStepInjector(pipeline)
                            .OnReceive(new StampMessageTypeHeaderStep<UserChangedEvent>(), PipelineRelativePosition.Before, typeof(DeserializeIncomingMessageStep));
                    });
                });
        }, key: "user", isDefaultBus: false);

        // Registers handlers for both TenantChangedEventHandler and UserChangedEventHandler
        // (same assembly). Safe to share across both buses: Rebus dispatches by message CLR
        // type, and each queue only ever carries the one message type it was declared for, so
        // there's no risk of a message being routed to the wrong handler.
        services.AutoRegisterHandlersFromAssemblyOf<TenantChangedEventHandler>();

        return services;
    }

    internal static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException(
                "RabbitMQ connection string not found. Expected Aspire to inject " +
                "ConnectionStrings:RabbitMq via .WithReference(rabbitmq) in AppHost.cs.");
}
