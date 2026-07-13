using Microsoft.AspNetCore.SignalR;

namespace OpenHub.Accessor.RealtimeMessaging.Tests.Fakes;

/// <summary>
/// Stand-in for OpenHub.RealtimeMessaging.Api's real NotificationHub — same method names/
/// signatures, but records invocations instead of dispatching through Manager/Engine/Accessor.
/// No [Authorize] here: this only proves the client accessor invokes the right hub method with
/// the right arguments and serializes payloads correctly. Real authorization enforcement is
/// covered by OpenHub.RealtimeMessaging.Api.Tests, against the real hub.
/// </summary>
public class RecordingHub(List<(string Method, object?[] Args)> invocations) : Hub
{
    public Task SendToTenant(string tenantSlug, string messageType, string payloadJson, string? correlationId)
    {
        invocations.Add(("SendToTenant", [tenantSlug, messageType, payloadJson, correlationId]));
        return Task.CompletedTask;
    }

    public Task SendToUser(string tenantSlug, string userId, string messageType, string payloadJson, string? correlationId)
    {
        invocations.Add(("SendToUser", [tenantSlug, userId, messageType, payloadJson, correlationId]));
        return Task.CompletedTask;
    }

    public Task Broadcast(string messageType, string payloadJson, string? correlationId)
    {
        invocations.Add(("Broadcast", [messageType, payloadJson, correlationId]));
        return Task.CompletedTask;
    }
}
