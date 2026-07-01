using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Api.Models.Responses.V1;

namespace OpenHub.ProductManager.Api.Controllers;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/products")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization()
            .WithTags("Products");

        group.MapGet("/", async (
            IProductManager manager,
            ProductMapper mapper,
            CancellationToken ct) =>
        {
            var dtos = await manager.GetAllAsync(ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        })
        .WithName("GetProducts")
        .WithSummary("List all products")
        .WithDescription("Returns all active, non-deleted products belonging to the authenticated tenant. Results are automatically scoped to the caller's tenant via Row Level Security.")
        .Produces<List<ProductResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IProductManager manager,
            ProductMapper mapper,
            CancellationToken ct) =>
        {
            var dto = await manager.GetByIdAsync(id, ct);
            return dto is null
                ? Results.NotFound()
                : Results.Ok(mapper.ToResponse(dto));
        })
        .WithName("GetProductById")
        .WithSummary("Get a product by ID")
        .WithDescription("Returns a single product. Returns 404 if the product does not exist, has been soft-deleted, or belongs to a different tenant.")
        .Produces<ProductResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            [FromBody] CreateProductRequest request,
            HttpContext http,
            IProductManager manager,
            ProductMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            try
            {
                var dto = await manager.CreateAsync(request, userId, ct);
                return Results.Created($"/api/v1/products/{dto.Id}", mapper.ToResponse(dto));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.ValidationProblem(ResolveLookupError(ex.Message));
            }
        })
        .WithName("CreateProduct")
        .WithSummary("Create a product")
        .WithDescription("Creates a new product in the caller's tenant catalog. SKU must be unique within the tenant. ManufacturerId and BrandId must correspond to entries owned by the caller's tenant; the remaining reference IDs must correspond to entries in the platform-wide reference tables. See GET /api/v1/lookups for valid values.")
        .Produces<ProductResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateProductRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateProductRequest request,
            HttpContext http,
            IProductManager manager,
            ProductMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            try
            {
                var dto = await manager.UpdateAsync(id, request, userId, ct);
                return dto is null
                    ? Results.NotFound()
                    : Results.Ok(mapper.ToResponse(dto));
            }
            catch (KeyNotFoundException ex) when (ex.Message.StartsWith("Manufacturer") || ex.Message.StartsWith("Brand"))
            {
                return Results.ValidationProblem(ResolveLookupError(ex.Message));
            }
        })
        .WithName("UpdateProduct")
        .WithSummary("Update a product")
        .WithDescription("Updates an existing product. All fields are optional — only fields included in the request body are modified (patch semantics). Returns 404 if the product does not exist or belongs to a different tenant.")
        .Produces<ProductResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<UpdateProductRequest>>();

        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext http,
            IProductManager manager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var deleted = await manager.DeleteAsync(id, userId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteProduct")
        .WithSummary("Delete a product")
        .WithDescription("Soft-deletes a product. The product is marked as deleted and will no longer appear in list or get-by-id results, but is retained in the database for audit and history purposes.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static Guid GetUserId(HttpContext http)
        => http.Items["UserId"] as Guid?
            ?? throw new InvalidOperationException("User context not set. Ensure TenantMiddleware is registered.");

    private static Dictionary<string, string[]> ResolveLookupError(string message)
    {
        var field = message.StartsWith("Manufacturer") ? "Manufacturer" : "Brand";
        return new Dictionary<string, string[]> { [field] = [message] };
    }
}
