using Asp.Versioning;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Responses.V1;

namespace OpenHub.ProductManager.Api.Controllers;

public static class LookupEndpoints
{
    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/lookups")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization()
            .WithTags("Lookups");

        group.MapGet("/", async (
            ILookupManager manager,
            LookupMapper mapper,
            CancellationToken ct) =>
        {
            var dto = await manager.GetAllAsync(ct);
            return Results.Ok(mapper.ToResponse(dto));
        })
        .WithName("GetLookups")
        .WithSummary("Get all lookup data for the product UI")
        .WithDescription("Returns every reference list needed to populate product create/edit forms in a single call: platform-wide reference data (condition types, availability statuses, units of measure, currencies, price types) plus the caller's tenant-specific manufacturers and brands.")
        .Produces<LookupsResponse>(StatusCodes.Status200OK);

        return app;
    }
}
