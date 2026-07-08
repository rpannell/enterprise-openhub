using Microsoft.AspNetCore.Authorization;

namespace OpenHub.Administration.Api.Auth;

/// <summary>
/// Requires "tenant-admin" or "platform-admin" — a platform-admin can do anything a
/// tenant-admin can, across any org. This policy only answers "does the caller hold an
/// admin role at all"; whether they're allowed to touch the specific <c>{realmSlug}</c>
/// in the route is enforced by the Manager (see <see cref="Extensions.ClaimsPrincipalExtensions"/>).
/// </summary>
public sealed class TenantAdminHandler : AuthorizationHandler<TenantAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TenantAdminRequirement requirement)
    {
        if (context.User.IsInRole("tenant-admin") || context.User.IsInRole("platform-admin"))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
