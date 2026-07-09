using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenHub.SalesOrg.Api.Managers.Interfaces;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Api.Models.Responses.V1;

namespace OpenHub.SalesOrg.Api.Controllers;

public static class SalesOrganizationUnitEndpoints
{
    public static IEndpointRouteBuilder MapSalesOrganizationUnitEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/sales-org-units")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization()
            .WithTags("SalesOrganizationUnits");

        group.MapGet("/", async (ISalesOrganizationUnitManager manager, SalesOrganizationUnitMapper mapper, CancellationToken ct) =>
        {
            var dtos = await manager.GetAllAsync(ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        })
        .WithName("GetSalesOrganizationUnits")
        .WithSummary("List sales organization units")
        .WithDescription("Returns every non-deleted organization unit belonging to the authenticated tenant, at any depth in the hierarchy.")
        .Produces<IReadOnlyList<SalesOrganizationUnitResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, ISalesOrganizationUnitManager manager, SalesOrganizationUnitMapper mapper, CancellationToken ct) =>
        {
            var dto = await manager.GetByIdAsync(id, ct);
            return dto is not null ? Results.Ok(mapper.ToResponse(dto)) : Results.NotFound();
        })
        .WithName("GetSalesOrganizationUnitById")
        .WithSummary("Get a sales organization unit by ID")
        .WithDescription("Returns a single organization unit. Returns 404 if the unit does not exist or belongs to a different tenant.")
        .Produces<SalesOrganizationUnitResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/descendants", async (Guid id, ISalesOrganizationUnitManager manager, CancellationToken ct) =>
        {
            var ids = await manager.GetDescendantsAsync(id, ct);
            return Results.Ok(ids);
        })
        .WithName("GetSalesOrganizationUnitDescendants")
        .WithSummary("List a unit's descendant unit IDs")
        .WithDescription("Returns every unit ID transitively below this unit in the hierarchy (its own subtree), via breadth-first traversal with a cycle guard.")
        .Produces<IReadOnlyList<Guid>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}/ancestors", async (Guid id, ISalesOrganizationUnitManager manager, CancellationToken ct) =>
        {
            var ids = await manager.GetAncestorsAsync(id, ct);
            return Results.Ok(ids);
        })
        .WithName("GetSalesOrganizationUnitAncestors")
        .WithSummary("List a unit's ancestor unit IDs")
        .WithDescription("Returns every unit ID above this unit in the hierarchy, nearest parent first, up to the root, with a cycle guard.")
        .Produces<IReadOnlyList<Guid>>(StatusCodes.Status200OK);

        group.MapPost("/", async (
            [FromBody] CreateSalesOrganizationUnitRequest request,
            HttpContext http,
            ISalesOrganizationUnitManager manager,
            SalesOrganizationUnitMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            try
            {
                var dto = await manager.CreateAsync(request, userId, ct);
                return Results.Created($"/api/v1/sales-org-units/{dto.Id}", mapper.ToResponse(dto));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["ParentOrganizationUnitId"] = [ex.Message] });
            }
        })
        .WithName("CreateSalesOrganizationUnit")
        .WithSummary("Create a sales organization unit")
        .WithDescription("Creates a new organization unit in the caller's tenant. ParentOrganizationUnitId, if given, must reference an existing unit — omit it to create a root/top-level unit.")
        .Produces<SalesOrganizationUnitResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateSalesOrganizationUnitRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSalesOrganizationUnitRequest request,
            HttpContext http,
            ISalesOrganizationUnitManager manager,
            SalesOrganizationUnitMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            try
            {
                var dto = await manager.UpdateAsync(id, request, userId, ct);
                return dto is null ? Results.NotFound() : Results.Ok(mapper.ToResponse(dto));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["ParentOrganizationUnitId"] = [ex.Message] });
            }
            catch (InvalidOperationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["ParentOrganizationUnitId"] = [ex.Message] });
            }
        })
        .WithName("UpdateSalesOrganizationUnit")
        .WithSummary("Update a sales organization unit")
        .WithDescription("Updates an existing organization unit. All fields are optional — only fields included in the request body are modified (patch semantics). Reparenting (changing ParentOrganizationUnitId) is rejected if it would create a cycle — the new parent cannot be a descendant of the unit being moved. Returns 404 if the unit does not exist or belongs to a different tenant.")
        .Produces<SalesOrganizationUnitResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<UpdateSalesOrganizationUnitRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISalesOrganizationUnitManager manager, CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var deleted = await manager.DeleteAsync(id, userId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSalesOrganizationUnit")
        .WithSummary("Delete a sales organization unit")
        .WithDescription("Soft-deletes an organization unit. The record is retained in the database for audit and history purposes.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static Guid GetUserId(HttpContext http)
        => http.Items["UserId"] as Guid?
            ?? throw new InvalidOperationException("User context not set. Ensure TenantMiddleware is registered.");
}
