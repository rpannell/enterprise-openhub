using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.CRM.Contracts;
using OpenHub.CRM.EventProcessor.Messaging;

namespace OpenHub.CRM.EventProcessor.Tests.Unit.Messaging;

public class ClientChangedEventHandlerTests
{
    [Fact]
    public async Task Handle_WellFormedMessage_CompletesWithoutThrowing()
    {
        var logger = Substitute.For<ILogger<ClientChangedEventHandler>>();
        var handler = new ClientChangedEventHandler(logger);
        var message = new ClientChangedEvent
        {
            ClientId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RealmSlug = "client-a",
            IsDeleted = false,
        };

        await Should.NotThrowAsync(() => handler.Handle(message));
    }

    [Fact]
    public async Task Handle_LogsReceiptOfTheEvent()
    {
        var logger = Substitute.For<ILogger<ClientChangedEventHandler>>();
        var handler = new ClientChangedEventHandler(logger);
        var message = new ClientChangedEvent
        {
            ClientId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RealmSlug = "client-a",
            IsDeleted = true,
        };

        await handler.Handle(message);

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(message.ClientId.ToString())),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
