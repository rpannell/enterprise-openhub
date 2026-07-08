using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Accessor.KeyCloak;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Models.Requests;
using OpenHub.Administration.Api.Tests.Integration.Fixtures;

namespace OpenHub.Administration.Api.Tests.Integration.Controllers;

[Trait("Category", "Integration")]
public class OrganizationEndpointsTests : IClassFixture<AdministrationApiFactory>
{
    private readonly AdministrationApiFactory _factory;

    public OrganizationEndpointsTests(AdministrationApiFactory factory)
    {
        _factory = factory;
    }

    private void AuthenticateAsPlatformAdmin()
        => _factory.ClaimsProvider.Claims =
        [
            new Claim(ClaimTypes.Role, "platform-admin"),
            new Claim("iss", "https://keycloak.test/realms/platform-admin"),
        ];

    private void AuthenticateAsTenantAdmin(string realmSlug = "client-a")
        => _factory.ClaimsProvider.Claims =
        [
            new Claim(ClaimTypes.Role, "tenant-admin"),
            new Claim("iss", $"https://keycloak.test/realms/{realmSlug}"),
        ];

    [Fact]
    public async Task CreateOrganization_AsPlatformAdmin_Returns201()
    {
        AuthenticateAsPlatformAdmin();
        _factory.OrganizationManagerMock.CreateOrganizationAsync(Arg.Any<CreateOrganizationDto>(), Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto { Id = "org-1", RealmSlug = "client-c", Name = "Client C", Alias = "client-c", Enabled = true });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest
        {
            RealmSlug = "client-c",
            Name = "Client C",
            Alias = "client-c",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateOrganization_AsTenantAdmin_Returns403()
    {
        AuthenticateAsTenantAdmin();

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest
        {
            RealmSlug = "client-c",
            Name = "Client C",
            Alias = "client-c",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateOrganization_Unauthenticated_Returns401()
    {
        _factory.ClaimsProvider.Claims = null;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest
        {
            RealmSlug = "client-c",
            Name = "Client C",
            Alias = "client-c",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateOrganization_RealmNotProvisioned_Returns400()
    {
        AuthenticateAsPlatformAdmin();
        _factory.OrganizationManagerMock.CreateOrganizationAsync(Arg.Any<CreateOrganizationDto>(), Arg.Any<CancellationToken>())
            .Returns<OrganizationDto>(_ => throw new InvalidOperationException("Realm 'client-z' does not exist."));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest
        {
            RealmSlug = "client-z",
            Name = "Client Z",
            Alias = "client-z",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrganization_DuplicateAlias_Returns409()
    {
        AuthenticateAsPlatformAdmin();
        _factory.OrganizationManagerMock.CreateOrganizationAsync(Arg.Any<CreateOrganizationDto>(), Arg.Any<CancellationToken>())
            .Returns<OrganizationDto>(_ => throw new KeyCloakAdminConflictException("Alias already exists."));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest
        {
            RealmSlug = "client-a",
            Name = "Client A",
            Alias = "client-a",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateOrganization_EmptyRealmSlug_ReturnsValidationProblem()
    {
        AuthenticateAsPlatformAdmin();

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest
        {
            RealmSlug = "",
            Name = "Client C",
            Alias = "client-c",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListOrganizations_AsPlatformAdmin_Returns200()
    {
        AuthenticateAsPlatformAdmin();
        _factory.OrganizationManagerMock.ListOrganizationsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new() { Id = "org-1", RealmSlug = "client-a", Name = "Client A", Alias = "client-a", Enabled = true } });

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/organizations");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOrganization_NotFound_Returns404()
    {
        AuthenticateAsPlatformAdmin();
        _factory.OrganizationManagerMock.GetOrganizationAsync("missing", Arg.Any<CancellationToken>())
            .Returns((OrganizationDto?)null);

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/organizations/missing");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
