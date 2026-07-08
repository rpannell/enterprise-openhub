using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Pipeline;
using OpenHub.ProductManager.Contracts;

namespace OpenHub.ProductManager.ElasticProcessor.Messaging.Serialization;

// The publisher (OpenHub.ProductManager.Api, via OpenHub.Accessor.RabbitMq) writes plain
// System.Text.Json ProductChangedEvent bytes with only "content-type: application/json" —
// it predates Rebus and sets no rbs2-* headers. Rebus's stock JSON deserializer requires
// rbs2-msg-type to know which CLR type to deserialize into and throws without it.
//
// This queue (product-manager-function.product-changed) is single-purpose: every message
// on it is always a ProductChangedEvent. So this step stamps the two headers Rebus's
// default deserializer needs directly onto the raw TransportMessage, before
// DeserializeIncomingMessageStep runs — leaving the rest of the default JSON pipeline
// untouched.
public class StampMessageTypeHeaderStep : IIncomingStep
{
    private static readonly string MessageTypeHeaderValue =
        typeof(ProductChangedEvent).GetSimpleAssemblyQualifiedName();

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
