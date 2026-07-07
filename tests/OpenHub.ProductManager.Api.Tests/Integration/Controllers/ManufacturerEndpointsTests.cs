using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Api.Models.Responses.V1;
using Shouldly;
using Xunit;

namespace OpenHub.ProductManager.Api.Tests.Integration.Controllers;

public class ManufacturerEndpointsTests : IClassFixture<ProductManagerApiFactory>
{
    private readonly ProductManagerApiFactory _factory;
    private readonly HttpClient _client;

    public ManufacturerEndpointsTests(ProductManagerApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateManufacturer_EmptyName_ReturnsBadRequestValidationProblem()
    {
        var request = new CreateManufacturerRequest { Name = string.Empty };

        var response = await _client.PostAsJsonAsync("/api/v1/manufacturers", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateManufacturer_ValidName_ReturnsCreated()
    {
        var manufacturerId = Guid.NewGuid();
        _factory.CatalogManager
            .CreateManufacturerAsync(Arg.Any<CreateManufacturerRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LookupItemDto { Id = manufacturerId, Name = "Acme Manufacturing" });

        var request = new CreateManufacturerRequest { Name = "Acme Manufacturing" };

        var response = await _client.PostAsJsonAsync("/api/v1/manufacturers", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<LookupItemResponse>();
        body.ShouldNotBeNull();
        body!.Id.ShouldBe(manufacturerId);
        body.Name.ShouldBe("Acme Manufacturing");
    }
}
