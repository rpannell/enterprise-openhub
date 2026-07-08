using NSubstitute;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Administration.Api.Managers;
using OpenHub.Administration.Api.Managers.Engines;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Mappers;
using Shouldly;
using Xunit;

namespace OpenHub.Administration.Api.Tests.Unit.Managers;

public class UserManagerTests
{
    private readonly IKeyCloakUserAccessor _userAccessor = Substitute.For<IKeyCloakUserAccessor>();
    private readonly IKeyCloakOrganizationAccessor _orgAccessor = Substitute.For<IKeyCloakOrganizationAccessor>();
    private readonly UserManager _sut;

    public UserManagerTests()
    {
        _sut = new UserManager(_userAccessor, _orgAccessor, new RoleAssignmentEngine(), new UserMapper(), new RoleMapper());
    }

    [Fact]
    public async Task ListUsersAsync_TenantAdminDifferentRealm_ThrowsForbiddenOperationException()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");

        await Should.ThrowAsync<ForbiddenOperationException>(() => _sut.ListUsersAsync(caller, "client-b"));
    }

    [Fact]
    public async Task ListUsersAsync_PlatformAdminAnyRealm_Succeeds()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: true, RealmSlug: "platform-admin");
        _userAccessor.ListUsersAsync("client-b", ct: Arg.Any<CancellationToken>()).Returns(new List<KeyCloakUserInfo>());

        var result = await _sut.ListUsersAsync(caller, "client-b");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task AssignRoleAsync_TenantAdminGrantingTenantAdminRole_ThrowsForbiddenOperationException()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");

        await Should.ThrowAsync<ForbiddenOperationException>(
            () => _sut.AssignRoleAsync(caller, "client-a", "user-1", "tenant-admin"));

        await _userAccessor.DidNotReceive().AssignRealmRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<KeyCloakRoleInfo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignRoleAsync_TenantAdminGrantingOrdinaryRole_Succeeds()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");
        _userAccessor.GetRealmRoleAsync("client-a", "product-manager", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRoleInfo { Id = "role-1", Name = "product-manager" });

        await _sut.AssignRoleAsync(caller, "client-a", "user-1", "product-manager");

        await _userAccessor.Received(1).AssignRealmRoleAsync(
            "client-a", "user-1", Arg.Is<KeyCloakRoleInfo>(r => r.Name == "product-manager"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignRoleAsync_RoleDoesNotExistInRealm_ThrowsInvalidOperationException()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");
        _userAccessor.GetRealmRoleAsync("client-a", "nonexistent", Arg.Any<CancellationToken>())
            .Returns((KeyCloakRoleInfo?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.AssignRoleAsync(caller, "client-a", "user-1", "nonexistent"));
    }

    [Fact]
    public async Task CreateUserAsync_AddsUserToOrganizationWhenOneExists()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");
        _userAccessor.CreateUserAsync("client-a", Arg.Any<CreateKeyCloakUserRequest>(), Arg.Any<CancellationToken>())
            .Returns("user-1");
        _orgAccessor.ListOrganizationsAsync("client-a", Arg.Any<CancellationToken>())
            .Returns(new List<KeyCloakOrganizationInfo> { new() { Id = "org-1", Name = "Client A", Alias = "client-a", Enabled = true, Domains = [] } });
        _userAccessor.GetUserByIdAsync("client-a", "user-1", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakUserInfo { Id = "user-1", Username = "newuser", Enabled = true });

        var request = new CreateUserDto { Username = "newuser", Email = "newuser@client-a.com" };
        var result = await _sut.CreateUserAsync(caller, "client-a", request);

        result.Id.ShouldBe("user-1");
        await _orgAccessor.Received(1).AddMemberAsync("client-a", "org-1", "user-1", Arg.Any<CancellationToken>());
    }
}
