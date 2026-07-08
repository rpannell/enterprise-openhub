using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Pipeline;

namespace OpenHub.ProductManager.IdentityEventProcessor.Messaging.Serialization;

// The Java Keycloak listener writes plain JSON bytes with no rbs2-* headers — same
// situation as ElasticProcessor's StampMessageTypeHeaderStep, see there for the full
// rationale. Generic here (rather than hardcoded to one type) because this worker has
// two single-purpose queues, each carrying exactly one message type — one instance of
// this step per bus, parameterized with that bus's message type.
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
