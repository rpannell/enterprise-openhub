using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenHub.SalesOrg.Api.Managers.Interfaces;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Api.Models.Responses.V1;

namespace OpenHub.SalesOrg.Api.Controllers;

public static class SalesRoleEndpoints
{
    public static IEndpointRouteBuilder MapSalesRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/sales-roles")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization()
            .WithTags("SalesRoles");

        group.MapGet("/", async (ISalesRoleManager manager, SalesRoleMapper mapper, CancellationToken ct) =>
        {
            var dtos = await manager.GetAllAsync(ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        })
        .WithName("GetSalesRoles")
        .WithSummary("List sales roles")
        .WithDescription("Returns every non-deleted sales role belonging to the authenticated tenant. Roles are tenant-configurable — there is no hardcoded role list.")
        .Produces<IReadOnlyList<SalesRoleResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, ISalesRoleManager manager, SalesRoleMapper mapper, CancellationToken ct) =>
        {
            var dto = await manager.GetByIdAsync(id, ct);
            return dto is not null ? Results.Ok(mapper.ToResponse(dto)) : Results.NotFound();
        })
        .WithName("GetSalesRoleById")
        .WithSummary("Get a sales role by ID")
        .WithDescription("Returns a single sales role. Returns 404 if the role does not exist or belongs to a different tenant.")
        .Produces<SalesRoleResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            [FromBody] CreateSalesRoleRequest request,
            ISalesRoleManager manager,
            SalesRoleMapper mapper,
            CancellationToken ct) =>
        {
            var dto = await manager.CreateAsync(request, ct);
            return Results.Created($"/api/v1/sales-roles/{dto.Id}", mapper.ToResponse(dto));
        })
        .WithName("CreateSalesRole")
        .WithSummary("Create a sales role")
        .WithDescription("Creates a new sales role in the caller's tenant. HierarchyLevel is optional — a role with no configured level never outranks anyone in the manager-chain algorithm.")
        .Produces<SalesRoleResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateSalesRoleRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSalesRoleRequest request,
            ISalesRoleManager manager,
            SalesRoleMapper mapper,
            CancellationToken ct) =>
        {
            var dto = await manager.UpdateAsync(id, request, ct);
            return dto is null ? Results.NotFound() : Results.Ok(mapper.ToResponse(dto));
        })
        .WithName("UpdateSalesRole")
        .WithSummary("Update a sales role")
        .WithDescription("Updates an existing sales role. All fields are optional — only fields included in the request body are modified (patch semantics). Returns 404 if the role does not exist or belongs to a different tenant.")
        .Produces<SalesRoleResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<UpdateSalesRoleRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, ISalesRoleManager manager, CancellationToken ct) =>
        {
            var deleted = await manager.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSalesRole")
        .WithSummary("Delete a sales role")
        .WithDescription("Soft-deletes a sales role. The record is retained in the database for audit and history purposes.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
