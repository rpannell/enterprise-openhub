using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Api.Models.Responses.V1;
using OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

namespace OpenHub.ProductManager.Api.Tests.Integration.Controllers;

[Trait("Category", "Integration")]
public class ManufacturerEndpointsTests : IClassFixture<ProductManagerApiFactory>
{
    private readonly ProductManagerApiFactory _factory;

    public ManufacturerEndpointsTests(ProductManagerApiFactory factory)
    {
        _factory = factory;
        var tenantId = Guid.NewGuid();
        _factory.ClaimsProvider.Claims =
        [
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("iss", "https://keycloak.example.com/realms/client-a"),
            new Claim("sub", "kc-user-1"),
        ];
        _factory.TenantAccessorMock
            .GetOrProvisionAsync(tenantId, "client-a", Arg.Any<CancellationToken>())
            .Returns(new TenantDto { Id = tenantId, Slug = "client-a", Name = "Client A", PlanTier = "standard", IsActive = true });
    }

    [Fact]
    public async Task CreateManufacturer_Valid_Returns201()
    {
        var created = new LookupItemDto { Id = Guid.NewGuid(), Name = "Acme" };
        _factory.CatalogManagerMock
            .CreateManufacturerAsync(Arg.Any<CreateManufacturerRequest>(), Arg.Any<CancellationToken>())
            .Returns(created);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/manufacturers/", new CreateManufacturerRequest { Name = "Acme" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<LookupItemResponse>();
        body!.Id.ShouldBe(created.Id);
        body.Name.ShouldBe("Acme");
    }

    [Fact]
    public async Task CreateManufacturer_Unauthenticated_Returns401()
    {
        _factory.ClaimsProvider.Claims = null;
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/manufacturers/", new CreateManufacturerRequest { Name = "Acme" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
