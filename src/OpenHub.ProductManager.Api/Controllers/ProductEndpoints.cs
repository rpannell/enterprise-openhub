using Microsoft.AspNetCore.Mvc;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Controllers;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products")
            .RequireAuthorization()
            .WithTags("Products");

        group.MapGet("/", async (
            IProductManager manager,
            ProductMapper mapper,
            CancellationToken ct) =>
        {
            var dtos = await manager.GetAllAsync(ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        });

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
        });

        group.MapPost("/", async (
            [FromBody] CreateProductRequest request,
            HttpContext http,
            IProductManager manager,
            ProductMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var dto = await manager.CreateAsync(request, userId, ct);
            return Results.Created($"/products/{dto.Id}", mapper.ToResponse(dto));
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateProductRequest request,
            HttpContext http,
            IProductManager manager,
            ProductMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var dto = await manager.UpdateAsync(id, request, userId, ct);
            return dto is null
                ? Results.NotFound()
                : Results.Ok(mapper.ToResponse(dto));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext http,
            IProductManager manager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var deleted = await manager.DeleteAsync(id, userId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }

    // Resolve the internal catalog.users UUID stored in HttpContext after TenantMiddleware + auth
    private static Guid GetUserId(HttpContext http)
    {
        var sub = http.User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User sub claim missing from JWT.");

        // Future: look up catalog.users.id by (tenant_id, unique_id) via UserAccessor.
        // For now, return a deterministic UUID derived from the sub so writes don't fail.
        return Guid.TryParse(sub, out var id) ? id : Guid.NewGuid();
    }
}
