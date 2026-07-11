using System.Security.Claims;

namespace OpenHub.RealtimeMessaging.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Derives the caller's own realm slug from the token issuer (".../realms/{slug}").</summary>
    public static string? GetRealmSlug(this ClaimsPrincipal user)
    {
        var issuer = user.FindFirst("iss")?.Value;
        if (string.IsNullOrEmpty(issuer)) return null;

        const string marker = "/realms/";
        var index = issuer.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;

        return issuer[(index + marker.Length)..].TrimEnd('/');
    }

    /// <summary>
    /// True when the token was issued by the dedicated "platform-admin" Keycloak realm.
    /// Unlike the interactive-user check elsewhere in the platform (which also requires the
    /// "platform-admin" realm role), this only checks the issuer: every caller reaching this
    /// method already passed the "RealtimeMessagingPublisher" scope-claim policy, so the only
    /// question left is which realm's M2M client secret produced the token. Keycloak service
    /// accounts don't carry interactive realm-role claims unless explicitly assigned one, so
    /// requiring the role here would reject legitimate M2M callers.
    /// </summary>
    public static bool IsPlatformAdmin(this ClaimsPrincipal user)
    {
        var issuer = user.FindFirst("iss")?.Value?.TrimEnd('/');
        return issuer is not null && issuer.EndsWith("/realms/platform-admin", StringComparison.OrdinalIgnoreCase);
    }
}
