using OpenHub.SalesOrg.Api.Accessors.Interfaces;

namespace OpenHub.SalesOrg.Api.Middleware;

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantAccessor tenantAccessor)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            var iss         = context.User.FindFirst("iss")?.Value;
            var sub         = context.User.FindFirst("sub")?.Value;
            var email       = context.User.FindFirst("email")?.Value ?? "";
            var firstName   = context.User.FindFirst("given_name")?.Value ?? "";
            var lastName    = context.User.FindFirst("family_name")?.Value ?? "";

            var realmSlug = iss?.Split("/realms/", 2).LastOrDefault()?.TrimEnd('/');

            if (Guid.TryParse(tenantClaim, out var tenantId) && realmSlug is not null)
            {
                var tenant = await tenantAccessor.GetOrProvisionAsync(
                    tenantId, realmSlug, context.RequestAborted);

                if (tenant is not null)
                {
                    context.Items["TenantId"] = tenant.Id;
                    context.Items["TenantSlug"] = realmSlug;

                    if (sub is not null)
                    {
                        var userId = await tenantAccessor.GetOrProvisionUserAsync(
                            tenant.Id, realmSlug, sub, email, firstName, lastName, context.RequestAborted);

                        if (userId is not null)
                            context.Items["UserId"] = userId.Value;
                    }
                }
            }
        }

        await next(context);
    }
}
