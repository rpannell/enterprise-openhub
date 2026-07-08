using System.Collections.Concurrent;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Messaging.Serialization;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Unit;

public class StampMessageTypeHeaderStepTests
{
    // ITransactionContext is irrelevant to this step (it only reads/mutates the
    // TransportMessage headers), but IncomingStepContext's constructor requires one.
    private sealed class FakeTransactionContext : ITransactionContext
    {
        public ConcurrentDictionary<string, object> Items { get; } = new();
        public void OnCommit(Func<ITransactionContext, Task> action) { }
        public void OnRollback(Func<ITransactionContext, Task> action) { }
        public void OnAck(Func<ITransactionContext, Task> action) { }
        public void OnNack(Func<ITransactionContext, Task> action) { }
        public void OnDisposed(Action<ITransactionContext> action) { }
        public void SetResult(bool commit, bool ack) { }
        public void Dispose() { }
    }

    private static IncomingStepContext CreateContext(Dictionary<string, string> headers)
    {
        var transportMessage = new TransportMessage(headers, [1, 2, 3]);
        var context = new IncomingStepContext(transportMessage, new FakeTransactionContext());
        return context;
    }

    [Fact]
    public async Task Process_HeaderlessMessage_StampsTypeAndContentTypeHeaders()
    {
        var headers = new Dictionary<string, string> { [Headers.ContentType] = "application/json" };
        var context = CreateContext(headers);
        var step = new StampMessageTypeHeaderStep();
        var nextCalled = false;

        await step.Process(context, () => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        var stamped = context.Load<TransportMessage>();
        stamped.Headers[Headers.Type].ShouldBe(typeof(ProductChangedEvent).GetSimpleAssemblyQualifiedName());
        stamped.Headers[Headers.ContentType].ShouldBe("application/json");
    }

    [Fact]
    public async Task Process_HeadersAlreadyPresent_DoesNotOverwriteThem()
    {
        var headers = new Dictionary<string, string>
        {
            [Headers.Type] = "some.other.Type, SomeAssembly",
            [Headers.ContentType] = "text/plain",
        };
        var context = CreateContext(headers);
        var step = new StampMessageTypeHeaderStep();

        await step.Process(context, () => Task.CompletedTask);

        var stamped = context.Load<TransportMessage>();
        stamped.Headers[Headers.Type].ShouldBe("some.other.Type, SomeAssembly");
        stamped.Headers[Headers.ContentType].ShouldBe("text/plain");
    }
}
