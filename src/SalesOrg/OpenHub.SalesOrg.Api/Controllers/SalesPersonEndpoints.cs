using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenHub.SalesOrg.Api.Managers.Interfaces;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Api.Models.Responses.V1;

namespace OpenHub.SalesOrg.Api.Controllers;

public static class SalesPersonEndpoints
{
    public static IEndpointRouteBuilder MapSalesPersonEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/sales-people")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization()
            .WithTags("SalesPeople");

        group.MapGet("/", async (ISalesPersonManager manager, SalesPersonMapper mapper, CancellationToken ct) =>
        {
            var dtos = await manager.GetAllAsync(ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        })
        .WithName("GetSalesPeople")
        .WithSummary("List sales people")
        .WithDescription("Returns every non-deleted sales person belonging to the authenticated tenant. Results are automatically scoped to the caller's tenant via Row Level Security.")
        .Produces<IReadOnlyList<SalesPersonResponse>>(StatusCodes.Status200OK);

        group.MapGet("/available-keycloak-users", async (ISalesPersonManager manager, SalesPersonMapper mapper, CancellationToken ct) =>
        {
            var dtos = await manager.GetAvailableKeycloakUsersAsync(ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        })
        .WithName("GetAvailableKeycloakUsersForSalesPerson")
        .WithSummary("List Keycloak users available to become a sales person")
        .WithDescription("Returns users in the caller's Keycloak realm who aren't already linked to a sales person. Backs the \"new sales person\" picker — sales people are always created by linking an existing Keycloak identity, not by typing name/email freehand, since they need to be able to log in.")
        .Produces<IReadOnlyList<AvailableKeycloakUserResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, ISalesPersonManager manager, SalesPersonMapper mapper, CancellationToken ct) =>
        {
            var dto = await manager.GetByIdAsync(id, ct);
            return dto is not null ? Results.Ok(mapper.ToResponse(dto)) : Results.NotFound();
        })
        .WithName("GetSalesPersonById")
        .WithSummary("Get a sales person by ID")
        .WithDescription("Returns a single sales person. Returns 404 if the person does not exist or belongs to a different tenant.")
        .Produces<SalesPersonResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/manager-chain", async (Guid id, ISalesPersonManager manager, SalesPersonMapper mapper, CancellationToken ct) =>
        {
            var dtos = await manager.GetManagerChainAsync(id, ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        })
        .WithName("GetSalesPersonManagerChain")
        .WithSummary("Get a sales person's manager chain")
        .WithDescription("Returns this person's manager chain, nearest link first: anyone who outranks them within their own org unit, then each successive parent unit's top-ranked assignee, up to the root. Derived from active+primary SalesAssignments and SalesRole.HierarchyLevel — not a stored manager pointer.")
        .Produces<IReadOnlyList<SalesPersonResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}/reports", async (Guid id, ISalesPersonManager manager, SalesPersonMapper mapper, CancellationToken ct) =>
        {
            var dtos = await manager.GetReportsAsync(id, ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        })
        .WithName("GetSalesPersonReports")
        .WithSummary("Get everyone reporting to a sales person")
        .WithDescription("Returns same-unit peers this person outranks, plus — if this person is their unit's top-ranked assignee — everyone assigned anywhere in that unit's descendant subtree.")
        .Produces<IReadOnlyList<SalesPersonResponse>>(StatusCodes.Status200OK);

        group.MapPost("/", async (
            [FromBody] CreateSalesPersonRequest request,
            HttpContext http,
            ISalesPersonManager manager,
            SalesPersonMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var dto = await manager.CreateAsync(request, userId, ct);
            return Results.Created($"/api/v1/sales-people/{dto.Id}", mapper.ToResponse(dto));
        })
        .WithName("CreateSalesPerson")
        .WithSummary("Create a sales person")
        .WithDescription("Creates a new sales person in the caller's tenant.")
        .Produces<SalesPersonResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateSalesPersonRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSalesPersonRequest request,
            HttpContext http,
            ISalesPersonManager manager,
            SalesPersonMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var dto = await manager.UpdateAsync(id, request, userId, ct);
            return dto is null ? Results.NotFound() : Results.Ok(mapper.ToResponse(dto));
        })
        .WithName("UpdateSalesPerson")
        .WithSummary("Update a sales person")
        .WithDescription("Updates an existing sales person. All fields are optional — only fields included in the request body are modified (patch semantics). Returns 404 if the person does not exist or belongs to a different tenant.")
        .Produces<SalesPersonResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<UpdateSalesPersonRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, ISalesPersonManager manager, CancellationToken ct) =>
        {
            var deleted = await manager.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSalesPerson")
        .WithSummary("Delete a sales person")
        .WithDescription("Soft-deletes a sales person. The record is retained in the database for audit and history purposes.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static Guid GetUserId(HttpContext http)
        => http.Items["UserId"] as Guid?
            ?? throw new InvalidOperationException("User context not set. Ensure TenantMiddleware is registered.");
}
