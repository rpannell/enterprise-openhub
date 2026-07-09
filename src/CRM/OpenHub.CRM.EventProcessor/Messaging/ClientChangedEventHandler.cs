using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using OpenHub.CRM.Contracts;

namespace OpenHub.CRM.EventProcessor.Messaging;

// Placeholder — logs receipt only. Grows real sync logic later (e.g. projecting clients into
// another datastore) without touching the topology/transport wiring above it.
public class ClientChangedEventHandler(ILogger<ClientChangedEventHandler> logger) : IHandleMessages<ClientChangedEvent>
{
    public Task Handle(ClientChangedEvent message)
    {
        logger.LogInformation(
            "Received ClientChangedEvent for client {ClientId} (tenant {TenantId}, deleted: {IsDeleted}).",
            message.ClientId, message.TenantId, message.IsDeleted);
        return Task.CompletedTask;
    }
}
