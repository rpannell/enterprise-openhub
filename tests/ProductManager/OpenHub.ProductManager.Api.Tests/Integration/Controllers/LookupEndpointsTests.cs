using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Models.Responses.V1;
using OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

namespace OpenHub.ProductManager.Api.Tests.Integration.Controllers;

[Trait("Category", "Integration")]
public class LookupEndpointsTests : IClassFixture<ProductManagerApiFactory>
{
    private readonly ProductManagerApiFactory _factory;

    public LookupEndpointsTests(ProductManagerApiFactory factory)
    {
        _factory = factory;
        _factory.ClaimsProvider.Claims =
        [
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim("sub", Guid.NewGuid().ToString()),
        ];
    }

    [Fact]
    public async Task GetLookups_Authenticated_Returns200_WithAggregatedShape()
    {
        _factory.LookupManagerMock.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new LookupsDto
        {
            ConditionTypes = [new LookupItemDto { Id = Guid.NewGuid(), Code = "NEW", Name = "New" }],
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/lookups");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LookupsResponse>();
        body.ShouldNotBeNull();
        body.ConditionTypes.ShouldHaveSingleItem();
        body.ConditionTypes[0].Name.ShouldBe("New");
    }

    [Fact]
    public async Task GetLookups_Unauthenticated_Returns401()
    {
        _factory.ClaimsProvider.Claims = null;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/lookups");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
