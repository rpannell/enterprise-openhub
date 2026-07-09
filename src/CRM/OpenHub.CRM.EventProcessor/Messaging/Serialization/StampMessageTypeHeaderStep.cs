using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Pipeline;

namespace OpenHub.CRM.EventProcessor.Messaging.Serialization;

// OpenHub.Accessor.RabbitMq's publisher writes plain JSON bytes with no rbs2-* headers
// (it's a hand-rolled publisher, not a Rebus bus) — Rebus needs Headers.Type to know which
// CLR type to deserialize into, so this stamps it in before deserialization runs. Same
// rationale as ProductManager.IdentityEventProcessor's identical step for Keycloak-originated
// events; this queue only ever carries ClientChangedEvent, so the type is hardcoded.
public class StampMessageTypeHeaderStep<TMessage> : IIncomingStep
{
    private static readonly string MessageTypeHeaderValue =
        typeof(TMessage).GetSimpleAssemblyQualifiedName();

    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        var transportMessage = context.Load<TransportMessage>();
        var headers = transportMessage.Headers;

        if (!headers.ContainsKey(Headers.Type))
        {
            headers[Headers.Type] = MessageTypeHeaderValue;
        }

        if (!headers.ContainsKey(Headers.ContentType))
        {
            headers[Headers.ContentType] = "application/json";
        }

        await next();
    }
}
