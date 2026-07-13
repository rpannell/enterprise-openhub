using OpenHub.RealtimeMessaging.Api.Managers.Engines.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers.Models;

namespace OpenHub.RealtimeMessaging.Api.Managers.Engines;

/// <summary>
/// Pure authorization rule, no infrastructure dependencies, for the two tenant paths
/// (SendToTenant/SendToUser): platform-admin callers may target any realm; everyone else may
/// only target their own realm. Broadcast has no Engine rule — that's gated solely by the
/// declarative RealtimeMessagingBroadcastPublisher policy on the Hub method.
/// </summary>
public class MessageTargetAuthorizationEngine : IMessageTargetAuthorizationEngine
{
    public DispatchAuthorizationResultDto Authorize(CallerContextDto caller, string targetRealmSlug)
    {
        if (caller.IsPlatformAdmin)
            return DispatchAuthorizationResultDto.Allow();

        if (string.IsNullOrEmpty(caller.RealmSlug))
            return DispatchAuthorizationResultDto.Deny("Caller has no resolvable realm.");

        return string.Equals(caller.RealmSlug, targetRealmSlug, StringComparison.OrdinalIgnoreCase)
            ? DispatchAuthorizationResultDto.Allow()
            : DispatchAuthorizationResultDto.Deny(
                $"Callers may only target their own realm ('{caller.RealmSlug}').");
    }
}
