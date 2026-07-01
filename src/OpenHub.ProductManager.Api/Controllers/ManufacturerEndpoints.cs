using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Api.Models.Responses.V1;

namespace OpenHub.ProductManager.Api.Controllers;

public static class ManufacturerEndpoints
{
    public static IEndpointRouteBuilder MapManufacturerEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/manufacturers")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization()
            .WithTags("Manufacturers");

        group.MapPost("/", async (
            [FromBody] CreateManufacturerRequest request,
            ICatalogManager manager,
            LookupMapper mapper,
            CancellationToken ct) =>
        {
            var dto = await manager.CreateManufacturerAsync(request, ct);
            return Results.Created($"/api/v1/manufacturers/{dto.Id}", mapper.ToResponse(dto));
        })
        .WithName("CreateManufacturer")
        .WithSummary("Create a manufacturer")
        .WithDescription("Creates a new manufacturer in the caller's tenant catalog.")
        .Produces<LookupItemResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateManufacturerRequest>>();

        return app;
    }
}
