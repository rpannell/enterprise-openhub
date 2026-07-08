using System.Security.Claims;

namespace OpenHub.Administration.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Derives the caller's own realm slug from the token issuer (".../realms/{slug}").</summary>
    public static string? GetRealmSlug(this ClaimsPrincipal user)
    {
        var issuer = user.FindFirst("iss")?.Value;
        if (string.IsNullOrEmpty(issuer)) return null;

        var marker = "/realms/";
        var index = issuer.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;

        return issuer[(index + marker.Length)..].TrimEnd('/');
    }

    /// <summary>The tenant_id claim delivered by each tenant realm's hardcoded protocol mapper.</summary>
    public static Guid? GetTenantId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : null;

    public static bool IsPlatformAdmin(this ClaimsPrincipal user) => user.IsInRole("platform-admin");
}
