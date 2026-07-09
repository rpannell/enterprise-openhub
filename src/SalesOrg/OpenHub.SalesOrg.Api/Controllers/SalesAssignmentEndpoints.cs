using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenHub.SalesOrg.Api.Managers.Interfaces;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Api.Models.Responses.V1;

namespace OpenHub.SalesOrg.Api.Controllers;

public static class SalesAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapSalesAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/sales-assignments")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization()
            .WithTags("SalesAssignments");

        group.MapGet("/", async (ISalesAssignmentManager manager, SalesAssignmentMapper mapper, CancellationToken ct) =>
        {
            var dtos = await manager.GetAllAsync(ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        })
        .WithName("GetSalesAssignments")
        .WithSummary("List sales assignments")
        .WithDescription("Returns every non-deleted assignment belonging to the authenticated tenant, most recent first, including closed/historical assignments (EffectiveEndDate set).")
        .Produces<IReadOnlyList<SalesAssignmentResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, ISalesAssignmentManager manager, SalesAssignmentMapper mapper, CancellationToken ct) =>
        {
            var dto = await manager.GetByIdAsync(id, ct);
            return dto is not null ? Results.Ok(mapper.ToResponse(dto)) : Results.NotFound();
        })
        .WithName("GetSalesAssignmentById")
        .WithSummary("Get a sales assignment by ID")
        .WithDescription("Returns a single assignment. Returns 404 if it does not exist or belongs to a different tenant.")
        .Produces<SalesAssignmentResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            [FromBody] CreateSalesAssignmentRequest request,
            HttpContext http,
            ISalesAssignmentManager manager,
            SalesAssignmentMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            try
            {
                var dto = await manager.CreateAsync(request, userId, ct);
                return Results.Created($"/api/v1/sales-assignments/{dto.Id}", mapper.ToResponse(dto));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["SalesPersonId"] = [ex.Message] });
            }
        })
        .WithName("CreateSalesAssignment")
        .WithSummary("Create a sales assignment")
        .WithDescription("Creates a new assignment of a sales person to an org unit and role. If PrimaryAssignmentFlag is true (the default), any prior active+primary assignment for the same person is automatically closed (EffectiveEndDate set, ActiveStatus cleared) rather than overwritten — history is preserved.")
        .Produces<SalesAssignmentResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateSalesAssignmentRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSalesAssignmentRequest request,
            HttpContext http,
            ISalesAssignmentManager manager,
            SalesAssignmentMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var dto = await manager.UpdateAsync(id, request, userId, ct);
            return dto is null ? Results.NotFound() : Results.Ok(mapper.ToResponse(dto));
        })
        .WithName("UpdateSalesAssignment")
        .WithSummary("Update a sales assignment")
        .WithDescription("Updates an existing assignment — typically to close it out (set EffectiveEndDate, clear ActiveStatus) rather than to move someone (which should instead create a new assignment). All fields are optional — only fields included in the request body are modified (patch semantics). Returns 404 if the assignment does not exist or belongs to a different tenant.")
        .Produces<SalesAssignmentResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<UpdateSalesAssignmentRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISalesAssignmentManager manager, CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var deleted = await manager.DeleteAsync(id, userId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSalesAssignment")
        .WithSummary("Delete a sales assignment")
        .WithDescription("Soft-deletes an assignment. The record is retained in the database for audit and history purposes.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static Guid GetUserId(HttpContext http)
        => http.Items["UserId"] as Guid?
            ?? throw new InvalidOperationException("User context not set. Ensure TenantMiddleware is registered.");
}
