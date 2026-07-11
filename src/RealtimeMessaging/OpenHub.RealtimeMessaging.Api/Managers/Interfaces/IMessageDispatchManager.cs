using OpenHub.RealtimeMessaging.Api.Managers.Models;

namespace OpenHub.RealtimeMessaging.Api.Managers.Interfaces;

public interface IMessageDispatchManager
{
    Task<DispatchAuthorizationResultDto> DispatchAsync(
        CallerContextDto caller, SendMessageDto message, CancellationToken ct = default);
}
