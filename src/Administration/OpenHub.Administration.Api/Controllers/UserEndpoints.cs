using Asp.Versioning;
using OpenHub.Administration.Api.Extensions;
using OpenHub.Administration.Api.Managers;
using OpenHub.Administration.Api.Managers.Interfaces;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Mappers;
using OpenHub.Administration.Api.Models.Requests;
using OpenHub.Administration.Api.Models.Responses.V1;

namespace OpenHub.Administration.Api.Controllers;

/// <summary>
/// Self-service user management, scoped to a single organization (Keycloak realm) —
/// gated by the "TenantAdminRole" policy. A tenant-admin may only manage their own
/// realm (enforced in the Manager, not here); a platform-admin may manage any realm.
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/organizations/{realmSlug}")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization("TenantAdminRole")
            .WithTags("Users");

        group.MapGet("/users", async (
            string realmSlug,
            HttpContext http,
            IUserManager manager,
            UserMapper mapper,
            CancellationToken ct) =>
        {
            try
            {
                var users = await manager.ListUsersAsync(CallerFrom(http), realmSlug, ct);
                return Results.Ok(users.Select(mapper.ToResponse));
            }
            catch (ForbiddenOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("ListOrganizationUsers")
        .WithSummary("List users in an organization")
        .WithDescription("Lists every user in the given organization's realm. A tenant-admin may only list their own organization; a platform-admin may list any.")
        .Produces<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/users", async (
            string realmSlug,
            CreateUserRequest request,
            HttpContext http,
            IUserManager manager,
            UserMapper mapper,
            CancellationToken ct) =>
        {
            try
            {
                var dto = await manager.CreateUserAsync(CallerFrom(http), realmSlug, mapper.ToDto(request), ct);
                return Results.Created($"/api/v1/organizations/{realmSlug}/users/{dto.Id}", mapper.ToResponse(dto));
            }
            catch (ForbiddenOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (OpenHub.Accessor.KeyCloak.KeyCloakAdminConflictException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("CreateOrganizationUser")
        .WithSummary("Create a user in an organization")
        .WithDescription("Creates a Keycloak user in the organization's realm, adds them as an organization member, and optionally assigns an initial role. " +
            "A tenant-admin may only create users in their own organization and cannot grant \"tenant-admin\" or \"platform-admin\" as the initial role.")
        .Produces<UserResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateUserRequest>>();

        group.MapPut("/users/{userId}", async (
            string realmSlug,
            string userId,
            UpdateUserRequest request,
            HttpContext http,
            IUserManager manager,
            UserMapper mapper,
            CancellationToken ct) =>
        {
            try
            {
                var dto = await manager.UpdateUserAsync(CallerFrom(http), realmSlug, userId, mapper.ToDto(request), ct);
                return Results.Ok(mapper.ToResponse(dto));
            }
            catch (ForbiddenOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("UpdateOrganizationUser")
        .WithSummary("Update a user in an organization")
        .WithDescription("Updates a user's profile fields and enabled status. Username is always kept in sync with email — " +
            "there is no separate username in this app. A tenant-admin may only update users in their own organization.")
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>();

        group.MapGet("/roles", async (
            string realmSlug,
            HttpContext http,
            IUserManager manager,
            RoleMapper mapper,
            CancellationToken ct) =>
        {
            try
            {
                var roles = await manager.ListAssignableRolesAsync(CallerFrom(http), realmSlug, ct);
                return Results.Ok(roles.Select(mapper.ToResponse));
            }
            catch (ForbiddenOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("ListAssignableRoles")
        .WithSummary("List roles the caller may assign")
        .WithDescription("Lists realm roles available in the organization. A tenant-admin never sees \"tenant-admin\" or \"platform-admin\" in this list — only a platform-admin can grant those.")
        .Produces<IReadOnlyList<RoleResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/users/{userId}/roles", async (
            string realmSlug,
            string userId,
            AssignRoleRequest request,
            HttpContext http,
            IUserManager manager,
            CancellationToken ct) =>
        {
            try
            {
                await manager.AssignRoleAsync(CallerFrom(http), realmSlug, userId, request.RoleName, ct);
                return Results.NoContent();
            }
            catch (ForbiddenOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("AssignOrganizationUserRole")
        .WithSummary("Assign a realm role to a user")
        .WithDescription("Assigns a realm role to a user in the organization. Rejected with 403 if the caller is a tenant-admin " +
            "attempting to grant \"tenant-admin\" or \"platform-admin\" — only a platform-admin may grant those roles.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<AssignRoleRequest>>();

        group.MapDelete("/users/{userId}/roles/{roleName}", async (
            string realmSlug,
            string userId,
            string roleName,
            HttpContext http,
            IUserManager manager,
            CancellationToken ct) =>
        {
            try
            {
                await manager.RemoveRoleAsync(CallerFrom(http), realmSlug, userId, roleName, ct);
                return Results.NoContent();
            }
            catch (ForbiddenOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("RemoveOrganizationUserRole")
        .WithSummary("Remove a realm role from a user")
        .WithDescription("Removes a realm role from a user in the organization. Rejected with 403 if the caller is a tenant-admin " +
            "attempting to remove \"tenant-admin\" or \"platform-admin\" — only a platform-admin may remove those.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static AdminCallerContext CallerFrom(HttpContext http) =>
        new(http.User.IsPlatformAdmin(), http.User.GetRealmSlug());
}
