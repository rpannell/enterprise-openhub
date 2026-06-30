using OpenHub.ProductManager.Api.Accessors.Interfaces;

namespace OpenHub.ProductManager.Api.Middleware;

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantAccessor tenantAccessor)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            var iss = context.User.FindFirst("iss")?.Value;

            // Extract realm slug from issuer: "http://keycloak:8080/realms/client-a" → "client-a"
            var realmSlug = iss?.Split("/realms/", 2).LastOrDefault()?.TrimEnd('/');

            if (Guid.TryParse(tenantClaim, out var tenantId) && realmSlug is not null)
            {
                var tenant = await tenantAccessor.GetOrProvisionAsync(
                    tenantId, realmSlug, context.RequestAborted);

                if (tenant is not null)
                    context.Items["TenantId"] = tenant.Id;
            }
        }

        await next(context);
    }
}
