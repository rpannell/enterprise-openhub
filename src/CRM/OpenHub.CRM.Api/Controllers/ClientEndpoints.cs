using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenHub.CRM.Api.Managers.Interfaces;
using OpenHub.CRM.Api.Mappers;
using OpenHub.CRM.Api.Models.Requests;
using OpenHub.CRM.Api.Models.Responses.V1;

namespace OpenHub.CRM.Api.Controllers;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/clients")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization()
            .WithTags("Clients");

        group.MapGet("/", async (IClientManager manager, ClientMapper mapper, CancellationToken ct) =>
        {
            var dtos = await manager.GetAllAsync(ct);
            return Results.Ok(mapper.ToResponseList(dtos));
        })
        .WithName("GetClients")
        .WithSummary("List clients")
        .WithDescription("Returns every non-deleted client belonging to the authenticated tenant, ordered by name. Results are automatically scoped to the caller's tenant via Row Level Security.")
        .Produces<IReadOnlyList<ClientResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IClientManager manager, ClientMapper mapper, CancellationToken ct) =>
        {
            var dto = await manager.GetByIdAsync(id, ct);
            return dto is not null ? Results.Ok(mapper.ToResponse(dto)) : Results.NotFound();
        })
        .WithName("GetClientById")
        .WithSummary("Get a client by ID")
        .WithDescription("Returns a single client, including its addresses and contacts. Returns 404 if the client does not exist or belongs to a different tenant.")
        .Produces<ClientResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            [FromBody] CreateClientRequest request,
            HttpContext http,
            IClientManager manager,
            ClientMapper mapper,
            CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            try
            {
                var dto = await manager.CreateAsync(request, userId, ct);
                return Results.Created($"/api/v1/clients/{dto.Id}", mapper.ToResponse(dto));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["SalesPersonId"] = [ex.Message] });
            }
        })
        .WithName("CreateClient")
        .WithSummary("Create a client")
        .WithDescription("Creates a new client in the caller's tenant. Also creates a matching Keycloak Organization in the tenant's realm and publishes a ClientChanged event to the client.changed RabbitMQ exchange. SalesPersonId must reference an existing sales person in SalesOrg.Api.")
        .Produces<ClientResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<CreateClientRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateClientRequest request,
            HttpContext http,
            IClientManager manager,
            ClientMapper mapper,
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
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["SalesPersonId"] = [ex.Message] });
            }
        })
        .WithName("UpdateClient")
        .WithSummary("Update a client")
        .WithDescription("Updates an existing client. All fields are optional — only fields included in the request body are modified (patch semantics). Publishes a ClientChanged event. Returns 404 if the client does not exist or belongs to a different tenant.")
        .Produces<ClientResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<UpdateClientRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, IClientManager manager, CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var deleted = await manager.DeleteAsync(id, userId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteClient")
        .WithSummary("Delete a client")
        .WithDescription("Soft-deletes a client and publishes a ClientChanged event with IsDeleted=true. The client is retained in the database for audit and history purposes.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static Guid GetUserId(HttpContext http)
        => http.Items["UserId"] as Guid?
            ?? throw new InvalidOperationException("User context not set. Ensure TenantMiddleware is registered.");
}
