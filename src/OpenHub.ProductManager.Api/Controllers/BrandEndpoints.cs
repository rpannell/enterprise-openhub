using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Api.Models.Responses.V1;

namespace OpenHub.ProductManager.Api.Controllers;

public static class BrandEndpoints
{
    public static IEndpointRouteBuilder MapBrandEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/brands")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization()
            .WithTags("Brands");

        group.MapPost("/", async (
            [FromBody] CreateBrandRequest request,
            ICatalogManager manager,
            LookupMapper mapper,
            CancellationToken ct) =>
        {
            var dto = await manager.CreateBrandAsync(request, ct);
            return Results.Created($"/api/v1/brands/{dto.Id}", mapper.ToResponse(dto));
        })
        .WithName("CreateBrand")
        .WithSummary("Create a brand")
        .WithDescription("Creates a new brand in the caller's tenant catalog. Brands are standalone — they are not linked to a manufacturer.")
        .Produces<LookupItemResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateBrandRequest>>();

        return app;
    }
}
