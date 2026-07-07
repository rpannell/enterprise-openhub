using Asp.Versioning;
using Microsoft.Extensions.Hosting;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Controllers;

/// <summary>
/// Platform-admin endpoints — cross-tenant by design, gated by the "AdminScope" policy
/// (a platform-admin M2M token) rather than the default per-tenant authorization applied
/// to every other controller. Not reachable via TenantMiddleware's tenant resolution, since
/// an admin token has no tenant_id claim.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/admin")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization("AdminScope")
            .WithTags("Admin");

        group.MapGet("/tenants", async (
            ITenantManager manager,
            CancellationToken ct) =>
        {
            var tenants = await manager.ListActiveTenantsAsync(ct);
            return Results.Ok(tenants);
        })
        .WithName("ListTenants")
        .WithSummary("List active tenants")
        .WithDescription("Returns every active tenant's id, slug, name, and plan tier. Platform-admin only — " +
            "requires a platform-admin M2M token (AdminScope), not a per-tenant user token. Used by the " +
            "scheduled full-catalog reindex job to enumerate tenants to process.")
        .Produces<IReadOnlyList<TenantDto>>(StatusCodes.Status200OK);

        group.MapPost("/tenants/sync", async (
            SyncTenantRequest request,
            ITenantManager manager,
            TenantMapper mapper,
            CancellationToken ct) =>
        {
            await manager.SyncTenantAsync(mapper.ToDto(request), ct);
            return Results.NoContent();
        })
        .WithName("SyncTenant")
        .WithSummary("Upsert or soft-delete a tenant")
        .WithDescription("Creates or updates the tenant row for the given tenant id, or soft-deletes it when " +
            "IsDeleted is true. Platform-admin only. Called by OpenHub.ProductManager.IdentityEventProcessor " +
            "when Keycloak reports an organization change on the tenant.changed fan-out exchange.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<SyncTenantRequest>>();

        group.MapPost("/users/sync", async (
            SyncUserRequest request,
            ITenantManager manager,
            TenantMapper mapper,
            CancellationToken ct) =>
        {
            await manager.SyncUserAsync(mapper.ToDto(request), ct);
            return Results.NoContent();
        })
        .WithName("SyncUser")
        .WithSummary("Upsert or soft-delete a user")
        .WithDescription("Creates or updates the user row for the given tenant/user id, or soft-deletes it " +
            "when IsDeleted is true. Platform-admin only. Called by OpenHub.ProductManager.IdentityEventProcessor " +
            "when Keycloak reports a user or organization-membership change on the user.changed fan-out exchange.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<SyncUserRequest>>();

        return app;
    }
}
