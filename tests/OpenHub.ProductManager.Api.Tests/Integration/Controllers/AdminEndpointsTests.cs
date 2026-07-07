using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

namespace OpenHub.ProductManager.Api.Tests.Integration.Controllers;

[Trait("Category", "Integration")]
public class AdminEndpointsTests : IClassFixture<ProductManagerApiFactory>
{
    private readonly ProductManagerApiFactory _factory;

    public AdminEndpointsTests(ProductManagerApiFactory factory)
    {
        _factory = factory;
    }

    private void AuthenticateAsAdmin()
        => _factory.ClaimsProvider.Claims =
        [
            new Claim("sub", "platform-admin-client"),
            new Claim("scope", "product-manager-admin"),
        ];

    [Fact]
    public async Task ListTenants_AsAdmin_Returns200()
    {
        AuthenticateAsAdmin();
        _factory.TenantManagerMock.ListActiveTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TenantDto> { new() { Id = Guid.NewGuid(), Slug = "client-a", Name = "Client A", PlanTier = "standard", IsActive = true } });

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tenants");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<TenantDto>>();
        body.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ListTenants_AuthenticatedWithoutAdminScope_Returns403()
    {
        _factory.ClaimsProvider.Claims = [new Claim("sub", "regular-user")];
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/tenants");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListTenants_Unauthenticated_Returns401()
    {
        _factory.ClaimsProvider.Claims = null;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/tenants");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SyncTenant_ValidRequest_AsAdmin_Returns204()
    {
        AuthenticateAsAdmin();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/tenants/sync", new SyncTenantRequest
        {
            TenantId = Guid.NewGuid(),
            RealmSlug = "client-a",
            Name = "Client A",
            IsDeleted = false,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _factory.TenantManagerMock.Received(1).SyncTenantAsync(Arg.Any<TenantSyncDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncTenant_EmptyRealmSlug_ReturnsBadRequestValidationProblem()
    {
        AuthenticateAsAdmin();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/tenants/sync", new SyncTenantRequest
        {
            TenantId = Guid.NewGuid(),
            RealmSlug = "",
            IsDeleted = false,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SyncUser_ValidRequest_AsAdmin_Returns204()
    {
        AuthenticateAsAdmin();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/users/sync", new SyncUserRequest
        {
            TenantId = Guid.NewGuid(),
            UniqueId = "kc-user-1",
            Email = "a@example.com",
            IsDeleted = false,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _factory.TenantManagerMock.Received(1).SyncUserAsync(Arg.Any<UserSyncDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUser_EmptyUniqueId_ReturnsBadRequestValidationProblem()
    {
        AuthenticateAsAdmin();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/users/sync", new SyncUserRequest
        {
            TenantId = Guid.NewGuid(),
            UniqueId = "",
            IsDeleted = false,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SyncUser_AuthenticatedWithoutAdminScope_Returns403()
    {
        _factory.ClaimsProvider.Claims = [new Claim("sub", "regular-user")];
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/users/sync", new SyncUserRequest
        {
            TenantId = Guid.NewGuid(),
            UniqueId = "kc-user-1",
            IsDeleted = false,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
