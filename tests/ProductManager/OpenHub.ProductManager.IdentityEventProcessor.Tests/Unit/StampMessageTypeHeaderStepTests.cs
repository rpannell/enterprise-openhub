using System.Collections.Concurrent;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;
using Shouldly;
using Xunit;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Messaging.Serialization;

namespace OpenHub.ProductManager.IdentityEventProcessor.Tests.Unit;

public class StampMessageTypeHeaderStepTests
{
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
        return new IncomingStepContext(transportMessage, new FakeTransactionContext());
    }

    [Fact]
    public async Task Process_HeaderlessTenantMessage_StampsTenantChangedEventType()
    {
        var headers = new Dictionary<string, string> { [Headers.ContentType] = "application/json" };
        var context = CreateContext(headers);
        var step = new StampMessageTypeHeaderStep<TenantChangedEvent>();

        await step.Process(context, () => Task.CompletedTask);

        var stamped = context.Load<TransportMessage>();
        stamped.Headers[Headers.Type].ShouldBe(typeof(TenantChangedEvent).GetSimpleAssemblyQualifiedName());
        stamped.Headers[Headers.ContentType].ShouldBe("application/json");
    }

    [Fact]
    public async Task Process_HeaderlessUserMessage_StampsUserChangedEventType()
    {
        var headers = new Dictionary<string, string>();
        var context = CreateContext(headers);
        var step = new StampMessageTypeHeaderStep<UserChangedEvent>();

        await step.Process(context, () => Task.CompletedTask);

        var stamped = context.Load<TransportMessage>();
        stamped.Headers[Headers.Type].ShouldBe(typeof(UserChangedEvent).GetSimpleAssemblyQualifiedName());
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
        var step = new StampMessageTypeHeaderStep<TenantChangedEvent>();

        await step.Process(context, () => Task.CompletedTask);

        var stamped = context.Load<TransportMessage>();
        stamped.Headers[Headers.Type].ShouldBe("some.other.Type, SomeAssembly");
        stamped.Headers[Headers.ContentType].ShouldBe("text/plain");
    }
}
