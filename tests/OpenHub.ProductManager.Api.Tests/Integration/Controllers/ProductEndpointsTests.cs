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
public class ProductEndpointsTests : IClassFixture<ProductManagerApiFactory>
{
    private readonly ProductManagerApiFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public ProductEndpointsTests(ProductManagerApiFactory factory)
    {
        _factory = factory;
        _factory.ClaimsProvider.Claims =
        [
            new Claim("tenant_id", _tenantId.ToString()),
            new Claim("iss", "https://keycloak.example.com/realms/client-a"),
            new Claim("sub", "kc-user-1"),
        ];

        // TenantMiddleware calls these directly on every authenticated request — must resolve
        // for [Authorize]'d product endpoints (esp. POST/PUT/DELETE, which read Items["UserId"])
        // to work at all.
        _factory.TenantAccessorMock
            .GetOrProvisionAsync(_tenantId, "client-a", Arg.Any<CancellationToken>())
            .Returns(new TenantDto { Id = _tenantId, Slug = "client-a", Name = "Client A", PlanTier = "standard", IsActive = true });
        _factory.TenantAccessorMock
            .GetOrProvisionUserAsync(_tenantId, "kc-user-1", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_userId);
    }

    [Fact]
    public async Task GetProducts_ValidPaging_Returns200_WithPagedResponse()
    {
        _factory.ProductManagerMock
            .GetPagedAsync(Arg.Any<ProductListRequest>(), Arg.Any<CancellationToken>())
            .Returns((new List<ProductDto> { new() { Id = Guid.NewGuid(), Sku = "SKU-1", Name = "Widget" } }, 1));

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/products/?page=1&pageSize=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        body.ShouldNotBeNull();
        body.TotalCount.ShouldBe(1);
        body.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetProducts_PageSizeOutOfRange_ReturnsBadRequestValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products/?page=1&pageSize=0");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProductById_Found_Returns200()
    {
        var productId = Guid.NewGuid();
        _factory.ProductManagerMock
            .GetByIdAsync(productId, Arg.Any<CancellationToken>())
            .Returns((new ProductDto { Id = productId, Sku = "SKU-1", Name = "Widget" }, false));

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/products/{productId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body!.Id.ShouldBe(productId);
    }

    [Fact]
    public async Task GetProductById_NotFound_Returns404()
    {
        _factory.ProductManagerMock
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(((ProductDto?)null, false));

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductById_Deleted_Returns410()
    {
        _factory.ProductManagerMock
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(((ProductDto?)null, true));

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task CreateProduct_Valid_Returns201()
    {
        var created = new ProductDto { Id = Guid.NewGuid(), Sku = "SKU-1", Name = "Widget" };
        _factory.ProductManagerMock
            .CreateAsync(Arg.Any<CreateProductRequest>(), _userId, Arg.Any<CancellationToken>())
            .Returns(created);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/products/", new CreateProductRequest
        {
            Sku = "SKU-1",
            Name = "Widget",
            Manufacturer = "Acme",
            Brand = "AcmeBrand",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body!.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task CreateProduct_MissingRequiredFields_ReturnsBadRequestValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/products/", new CreateProductRequest
        {
            Sku = "",
            Name = "",
            Manufacturer = "",
            Brand = "",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProduct_Found_Returns200()
    {
        var productId = Guid.NewGuid();
        _factory.ProductManagerMock
            .UpdateAsync(productId, Arg.Any<UpdateProductRequest>(), _userId, Arg.Any<CancellationToken>())
            .Returns(new ProductDto { Id = productId, Sku = "SKU-1", Name = "Renamed" });

        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync($"/api/v1/products/{productId}", new UpdateProductRequest { Name = "Renamed" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateProduct_NotFound_Returns404()
    {
        _factory.ProductManagerMock
            .UpdateAsync(Arg.Any<Guid>(), Arg.Any<UpdateProductRequest>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProductDto?)null);

        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync($"/api/v1/products/{Guid.NewGuid()}", new UpdateProductRequest { Name = "Renamed" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProduct_Found_Returns204()
    {
        var productId = Guid.NewGuid();
        _factory.ProductManagerMock
            .DeleteAsync(productId, _userId, Arg.Any<CancellationToken>())
            .Returns(true);

        var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/v1/products/{productId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteProduct_NotFound_Returns404()
    {
        _factory.ProductManagerMock
            .DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/v1/products/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProducts_Unauthenticated_Returns401()
    {
        _factory.ClaimsProvider.Claims = null;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products/");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
