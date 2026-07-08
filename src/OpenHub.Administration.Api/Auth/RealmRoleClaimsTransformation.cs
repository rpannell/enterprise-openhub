using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace OpenHub.Administration.Api.Auth;

/// <summary>
/// Keycloak delivers realm roles as a nested <c>realm_access: {"roles":[...]}</c> JSON
/// claim, not individual <see cref="ClaimTypes.Role"/> claims — ASP.NET Core's JWT bearer
/// handler does not flatten this automatically (confirmed: <c>MapInboundClaims = false</c>
/// in <c>JwtAuthenticationExtensions</c> only affects top-level claim-type renaming, not
/// nested JSON). This transform adds one <see cref="ClaimTypes.Role"/> claim per role so
/// <see cref="ClaimsPrincipal.IsInRole"/> and ASP.NET Core authorization policies work.
/// </summary>
public sealed class RealmRoleClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return Task.FromResult(principal);

        // Re-entrancy safe — IClaimsTransformation can run more than once per request.
        if (identity.HasClaim(c => c.Type == ClaimTypes.Role))
            return Task.FromResult(principal);

        var realmAccess = identity.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
            return Task.FromResult(principal);

        try
        {
            using var doc = JsonDocument.Parse(realmAccess);
            if (doc.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
                foreach (var role in roles.EnumerateArray())
                    if (role.GetString() is { Length: > 0 } roleName)
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
        }
        catch (JsonException)
        {
            // Malformed realm_access shouldn't fail the whole request — treat as "no roles".
        }

        return Task.FromResult(principal);
    }
}
