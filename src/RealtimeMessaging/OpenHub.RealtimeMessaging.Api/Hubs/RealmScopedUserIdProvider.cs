using Microsoft.AspNetCore.SignalR;
using OpenHub.RealtimeMessaging.Api.Auth;

namespace OpenHub.RealtimeMessaging.Api.Hubs;

/// <summary>
/// Backs SignalR's built-in per-user addressing (<c>Clients.User(...)</c>) with a realm-scoped
/// identifier ("{realmSlug}:{sub}") instead of the default ClaimTypes.NameIdentifier — this
/// service keys identity by realm+subject, not a single global user-id claim.
/// </summary>
public class RealmScopedUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var user = connection.User;
        var realmSlug = user?.GetRealmSlug();
        var sub = user?.FindFirst("sub")?.Value;

        return !string.IsNullOrEmpty(realmSlug) && !string.IsNullOrEmpty(sub)
            ? $"{realmSlug}:{sub}"
            : null;
    }
}
