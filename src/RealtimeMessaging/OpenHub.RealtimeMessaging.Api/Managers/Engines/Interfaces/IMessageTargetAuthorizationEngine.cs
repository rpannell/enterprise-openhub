using OpenHub.RealtimeMessaging.Api.Managers.Models;

namespace OpenHub.RealtimeMessaging.Api.Managers.Engines.Interfaces;

public interface IMessageTargetAuthorizationEngine
{
    DispatchAuthorizationResultDto Authorize(CallerContextDto caller, string targetRealmSlug);
}
