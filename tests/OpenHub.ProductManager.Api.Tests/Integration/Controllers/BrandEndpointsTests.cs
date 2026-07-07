using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Api.Models.Responses.V1;
using Shouldly;
using Xunit;

namespace OpenHub.ProductManager.Api.Tests.Integration.Controllers;

public class BrandEndpointsTests : IClassFixture<ProductManagerApiFactory>
{
    private readonly ProductManagerApiFactory _factory;
    private readonly HttpClient _client;

    public BrandEndpointsTests(ProductManagerApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateBrand_EmptyName_ReturnsBadRequestValidationProblem()
    {
        var request = new CreateBrandRequest { Name = string.Empty };

        var response = await _client.PostAsJsonAsync("/api/v1/brands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBrand_ValidName_ReturnsCreated()
    {
        var brandId = Guid.NewGuid();
        _factory.CatalogManager
            .CreateBrandAsync(Arg.Any<CreateBrandRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LookupItemDto { Id = brandId, Name = "Acme" });

        var request = new CreateBrandRequest { Name = "Acme" };

        var response = await _client.PostAsJsonAsync("/api/v1/brands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<LookupItemResponse>();
        body.ShouldNotBeNull();
        body!.Id.ShouldBe(brandId);
        body.Name.ShouldBe("Acme");
    }
}
