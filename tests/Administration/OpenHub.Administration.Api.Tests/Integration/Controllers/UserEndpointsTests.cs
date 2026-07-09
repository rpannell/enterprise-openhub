using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Administration.Api.Managers;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Models.Requests;
using OpenHub.Administration.Api.Tests.Integration.Fixtures;

namespace OpenHub.Administration.Api.Tests.Integration.Controllers;

[Trait("Category", "Integration")]
public class UserEndpointsTests : IClassFixture<AdministrationApiFactory>
{
    private readonly AdministrationApiFactory _factory;

    public UserEndpointsTests(AdministrationApiFactory factory)
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
    public async Task ListUsers_AsTenantAdmin_OwnRealm_Returns200()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.ListUsersAsync(Arg.Any<AdminCallerContext>(), "client-a", Arg.Any<CancellationToken>())
            .Returns(new List<UserDto>());

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/organizations/client-a/users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListUsers_Unauthenticated_Returns401()
    {
        _factory.ClaimsProvider.Claims = null;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/organizations/client-a/users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListUsers_AuthenticatedWithoutAdminRole_Returns403()
    {
        _factory.ClaimsProvider.Claims = [new Claim(ClaimTypes.Role, "user")];

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/organizations/client-a/users");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUser_AsTenantAdmin_DifferentOrgInRoute_Returns403()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.CreateUserAsync(Arg.Any<AdminCallerContext>(), "client-b", Arg.Any<CreateUserDto>(), Arg.Any<CancellationToken>())
            .Returns<UserDto>(_ => throw new ForbiddenOperationException("tenant-admin cannot manage organization 'client-b'."));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations/client-b/users", new CreateUserRequest
        {
            Email = "newuser@client-b.com",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUser_AsTenantAdmin_OwnOrg_Returns201()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.CreateUserAsync(Arg.Any<AdminCallerContext>(), "client-a", Arg.Any<CreateUserDto>(), Arg.Any<CancellationToken>())
            .Returns(new UserDto { Id = "user-1", Username = "newuser@client-a.com", Enabled = true, TemporaryPassword = "Tmp-abc123!" });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations/client-a/users", new CreateUserRequest
        {
            Email = "newuser@client-a.com",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateUser_ReturnsTemporaryPasswordInResponseBody()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.CreateUserAsync(Arg.Any<AdminCallerContext>(), "client-a", Arg.Any<CreateUserDto>(), Arg.Any<CancellationToken>())
            .Returns(new UserDto { Id = "user-1", Username = "newuser@client-a.com", Enabled = true, TemporaryPassword = "Tmp-abc123!" });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations/client-a/users", new CreateUserRequest
        {
            Email = "newuser@client-a.com",
        });

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Tmp-abc123!");
    }

    [Fact]
    public async Task CreateUser_EmptyEmail_ReturnsValidationProblem()
    {
        AuthenticateAsTenantAdmin("client-a");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations/client-a/users", new CreateUserRequest
        {
            Email = "",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUser_AsTenantAdmin_OwnOrg_Returns200()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.UpdateUserAsync(Arg.Any<AdminCallerContext>(), "client-a", "user-1", Arg.Any<UpdateUserDto>(), Arg.Any<CancellationToken>())
            .Returns(new UserDto { Id = "user-1", Username = "updated@client-a.com", Enabled = true });

        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/v1/organizations/client-a/users/user-1", new UpdateUserRequest
        {
            Email = "updated@client-a.com",
            Enabled = true,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUser_AsTenantAdmin_DifferentOrgInRoute_Returns403()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.UpdateUserAsync(Arg.Any<AdminCallerContext>(), "client-b", "user-1", Arg.Any<UpdateUserDto>(), Arg.Any<CancellationToken>())
            .Returns<UserDto>(_ => throw new ForbiddenOperationException("tenant-admin cannot manage organization 'client-b'."));

        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/v1/organizations/client-b/users/user-1", new UpdateUserRequest
        {
            Email = "updated@client-b.com",
            Enabled = true,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateUser_InvalidEmail_ReturnsValidationProblem()
    {
        AuthenticateAsTenantAdmin("client-a");

        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/v1/organizations/client-a/users/user-1", new UpdateUserRequest
        {
            Email = "not-an-email",
            Enabled = true,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignRole_TenantAdminAssigningTenantAdminRole_Returns403()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.AssignRoleAsync(Arg.Any<AdminCallerContext>(), "client-a", "user-1", "tenant-admin", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ForbiddenOperationException("Only a platform-admin can grant the 'tenant-admin' role.")));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations/client-a/users/user-1/roles", new AssignRoleRequest
        {
            RoleName = "tenant-admin",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignRole_TenantAdminAssigningPlatformAdminRole_Returns403()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.AssignRoleAsync(Arg.Any<AdminCallerContext>(), "client-a", "user-1", "platform-admin", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ForbiddenOperationException("Only a platform-admin can grant the 'platform-admin' role.")));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations/client-a/users/user-1/roles", new AssignRoleRequest
        {
            RoleName = "platform-admin",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignRole_TenantAdminAssigningOrdinaryRole_Returns204()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.AssignRoleAsync(Arg.Any<AdminCallerContext>(), "client-a", "user-1", "product-manager", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations/client-a/users/user-1/roles", new AssignRoleRequest
        {
            RoleName = "product-manager",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignRole_PlatformAdminAssigningTenantAdminRole_Returns204()
    {
        AuthenticateAsPlatformAdmin();
        _factory.UserManagerMock.AssignRoleAsync(Arg.Any<AdminCallerContext>(), "client-a", "user-1", "tenant-admin", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/organizations/client-a/users/user-1/roles", new AssignRoleRequest
        {
            RoleName = "tenant-admin",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveRole_TenantAdminRemovingTenantAdminRole_Returns403()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.RemoveRoleAsync(Arg.Any<AdminCallerContext>(), "client-a", "user-1", "tenant-admin", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ForbiddenOperationException("Only a platform-admin can grant the 'tenant-admin' role.")));

        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/v1/organizations/client-a/users/user-1/roles/tenant-admin");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveRole_TenantAdminRemovingOrdinaryRole_Returns204()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.RemoveRoleAsync(Arg.Any<AdminCallerContext>(), "client-a", "user-1", "product-manager", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/v1/organizations/client-a/users/user-1/roles/product-manager");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListAssignableRoles_AsTenantAdmin_Returns200()
    {
        AuthenticateAsTenantAdmin("client-a");
        _factory.UserManagerMock.ListAssignableRolesAsync(Arg.Any<AdminCallerContext>(), "client-a", Arg.Any<CancellationToken>())
            .Returns(new List<RoleDto> { new() { Name = "product-manager" } });

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/organizations/client-a/roles");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
