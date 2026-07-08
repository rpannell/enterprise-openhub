using Asp.Versioning;
using OpenHub.Accessor.KeyCloak;
using OpenHub.Administration.Api.Managers.Interfaces;
using OpenHub.Administration.Api.Mappers;
using OpenHub.Administration.Api.Models.Requests;

namespace OpenHub.Administration.Api.Controllers;

/// <summary>
/// Cross-tenant organization management — gated by the "PlatformAdminRole" policy
/// (OpenHub staff only, via the dedicated platform-admin Keycloak realm). Creates a
/// Keycloak Organization inside an already-provisioned realm; realm creation itself
/// stays a manual ops step (see README).
/// </summary>
public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/organizations")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization("PlatformAdminRole")
            .WithTags("Organizations");

        group.MapPost("/", async (
            CreateOrganizationRequest request,
            IOrganizationManager manager,
            OrganizationMapper mapper,
            CancellationToken ct) =>
        {
            try
            {
                var dto = await manager.CreateOrganizationAsync(mapper.ToDto(request), ct);
                return Results.Created($"/api/v1/organizations/{dto.RealmSlug}", mapper.ToResponse(dto));
            }
            catch (KeyCloakAdminConflictException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("CreateOrganization")
        .WithSummary("Create an organization")
        .WithDescription("Creates a Keycloak Organization inside an already-provisioned realm and registers it as a tenant. " +
            "Realm creation is a manual ops step and is not performed by this endpoint — the target realm must already exist. " +
            "Platform-admin only.")
        .Produces<Models.Responses.V1.OrganizationResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateOrganizationRequest>>();

        group.MapGet("/", async (
            IOrganizationManager manager,
            OrganizationMapper mapper,
            CancellationToken ct) =>
        {
            var organizations = await manager.ListOrganizationsAsync(ct);
            return Results.Ok(organizations.Select(mapper.ToResponse));
        })
        .WithName("ListOrganizations")
        .WithSummary("List organizations")
        .WithDescription("Lists every organization across every provisioned tenant realm. Platform-admin only.")
        .Produces<IReadOnlyList<Models.Responses.V1.OrganizationResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{realmSlug}", async (
            string realmSlug,
            IOrganizationManager manager,
            OrganizationMapper mapper,
            CancellationToken ct) =>
        {
            var dto = await manager.GetOrganizationAsync(realmSlug, ct);
            return dto is null ? Results.NotFound() : Results.Ok(mapper.ToResponse(dto));
        })
        .WithName("GetOrganization")
        .WithSummary("Get an organization by realm slug")
        .WithDescription("Returns the organization belonging to the given realm. Returns 404 if the realm has no organization yet. Platform-admin only.")
        .Produces<Models.Responses.V1.OrganizationResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
