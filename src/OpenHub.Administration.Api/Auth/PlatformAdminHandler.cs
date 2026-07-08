using Microsoft.AspNetCore.Authorization;

namespace OpenHub.Administration.Api.Auth;

/// <summary>
/// Requires the "platform-admin" realm role AND that the token was issued by the
/// platform-admin realm — the issuer check is defense in depth against the role name
/// ever being assigned inside a tenant realm by mistake (that realm role should exist
/// nowhere else). Matches the "platform-admin" realm slug convention already used by
/// <c>OpenHub.ProductManager.IdentityEventProcessor</c>.
/// </summary>
public sealed class PlatformAdminHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    private const string PlatformAdminRealmSlug = "platform-admin";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        var issuer = context.User.FindFirst("iss")?.Value?.TrimEnd('/');
        var issuedByPlatformAdminRealm = issuer is not null
            && issuer.EndsWith($"/realms/{PlatformAdminRealmSlug}", StringComparison.OrdinalIgnoreCase);

        if (issuedByPlatformAdminRealm && context.User.IsInRole("platform-admin"))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
