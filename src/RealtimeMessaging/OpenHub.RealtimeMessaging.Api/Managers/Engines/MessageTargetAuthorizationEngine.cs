using OpenHub.RealtimeMessaging.Api.Managers.Engines.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers.Models;
using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Managers.Engines;

/// <summary>
/// Pure authorization rule, no infrastructure dependencies: Broadcast requires a platform-admin
/// caller; Tenant/User targets require the caller's own realm to match the target realm, unless
/// the caller is platform-admin (who may target any realm).
/// </summary>
public class MessageTargetAuthorizationEngine : IMessageTargetAuthorizationEngine
{
    public DispatchAuthorizationResultDto Authorize(CallerContextDto caller, SendMessageDto message)
    {
        if (message.Target == MessageTarget.Broadcast)
            return caller.IsPlatformAdmin
                ? DispatchAuthorizationResultDto.Allow()
                : DispatchAuthorizationResultDto.Deny("Broadcasting to every tenant requires a platform-admin caller.");

        if (caller.IsPlatformAdmin)
            return DispatchAuthorizationResultDto.Allow();

        if (string.IsNullOrEmpty(caller.RealmSlug))
            return DispatchAuthorizationResultDto.Deny("Caller has no resolvable realm.");

        return string.Equals(caller.RealmSlug, message.TenantSlug, StringComparison.OrdinalIgnoreCase)
            ? DispatchAuthorizationResultDto.Allow()
            : DispatchAuthorizationResultDto.Deny(
                $"Tenant-scoped callers may only target their own realm ('{caller.RealmSlug}').");
    }
}
