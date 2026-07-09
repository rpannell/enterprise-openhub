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
            .Returns(new KeyCloakUserInfo { Id = "user-1", Username = "newuser@client-a.com", Enabled = true });

        var request = new CreateUserDto { Email = "newuser@client-a.com" };
        var result = await _sut.CreateUserAsync(caller, "client-a", request);

        result.Id.ShouldBe("user-1");
        await _orgAccessor.Received(1).AddMemberAsync("client-a", "org-1", "user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateUserAsync_UsesEmailAsKeycloakUsername()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");
        _userAccessor.CreateUserAsync("client-a", Arg.Any<CreateKeyCloakUserRequest>(), Arg.Any<CancellationToken>())
            .Returns("user-1");
        _userAccessor.GetUserByIdAsync("client-a", "user-1", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakUserInfo { Id = "user-1", Username = "newuser@client-a.com", Enabled = true });

        var request = new CreateUserDto { Email = "newuser@client-a.com" };
        await _sut.CreateUserAsync(caller, "client-a", request);

        await _userAccessor.Received(1).CreateUserAsync(
            "client-a",
            Arg.Is<CreateKeyCloakUserRequest>(r => r.Username == "newuser@client-a.com" && r.Email == "newuser@client-a.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateUserAsync_ReturnsGeneratedTemporaryPassword()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");
        _userAccessor.CreateUserAsync("client-a", Arg.Any<CreateKeyCloakUserRequest>(), Arg.Any<CancellationToken>())
            .Returns("user-1");
        _userAccessor.GetUserByIdAsync("client-a", "user-1", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakUserInfo { Id = "user-1", Username = "newuser@client-a.com", Enabled = true });

        var request = new CreateUserDto { Email = "newuser@client-a.com" };
        var result = await _sut.CreateUserAsync(caller, "client-a", request);

        result.TemporaryPassword.ShouldNotBeNullOrWhiteSpace();
        // The same value handed to Keycloak must be the one returned to the caller —
        // otherwise the admin would be shown a password that doesn't actually work.
        await _userAccessor.Received(1).CreateUserAsync(
            "client-a",
            Arg.Is<CreateKeyCloakUserRequest>(r => r.TemporaryPassword == result.TemporaryPassword),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateUserAsync_MultipleInitialRoles_AssignsEachOne()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: true, RealmSlug: "platform-admin");
        _userAccessor.CreateUserAsync("client-a", Arg.Any<CreateKeyCloakUserRequest>(), Arg.Any<CancellationToken>())
            .Returns("user-1");
        _userAccessor.GetUserByIdAsync("client-a", "user-1", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakUserInfo { Id = "user-1", Username = "newuser@client-a.com", Enabled = true });
        _userAccessor.GetRealmRoleAsync("client-a", "product-manager", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRoleInfo { Id = "role-1", Name = "product-manager" });
        _userAccessor.GetRealmRoleAsync("client-a", "warehouse-staff", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRoleInfo { Id = "role-2", Name = "warehouse-staff" });

        var request = new CreateUserDto
        {
            Email = "newuser@client-a.com",
            InitialRoles = ["product-manager", "warehouse-staff"]
        };
        var result = await _sut.CreateUserAsync(caller, "client-a", request);

        result.Roles.ShouldBe(["product-manager", "warehouse-staff"]);
        await _userAccessor.Received(1).AssignRealmRoleAsync(
            "client-a", "user-1", Arg.Is<KeyCloakRoleInfo>(r => r.Name == "product-manager"), Arg.Any<CancellationToken>());
        await _userAccessor.Received(1).AssignRealmRoleAsync(
            "client-a", "user-1", Arg.Is<KeyCloakRoleInfo>(r => r.Name == "warehouse-staff"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveRoleAsync_TenantAdminRemovingTenantAdminRole_ThrowsForbiddenOperationException()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");

        await Should.ThrowAsync<ForbiddenOperationException>(
            () => _sut.RemoveRoleAsync(caller, "client-a", "user-1", "tenant-admin"));

        await _userAccessor.DidNotReceive().RemoveRealmRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<KeyCloakRoleInfo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveRoleAsync_OrdinaryRole_Succeeds()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");
        _userAccessor.GetRealmRoleAsync("client-a", "product-manager", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRoleInfo { Id = "role-1", Name = "product-manager" });

        await _sut.RemoveRoleAsync(caller, "client-a", "user-1", "product-manager");

        await _userAccessor.Received(1).RemoveRealmRoleAsync(
            "client-a", "user-1", Arg.Is<KeyCloakRoleInfo>(r => r.Name == "product-manager"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveRoleAsync_RoleDoesNotExistInRealm_ThrowsInvalidOperationException()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");
        _userAccessor.GetRealmRoleAsync("client-a", "nonexistent", Arg.Any<CancellationToken>())
            .Returns((KeyCloakRoleInfo?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.RemoveRoleAsync(caller, "client-a", "user-1", "nonexistent"));
    }

    [Fact]
    public async Task UpdateUserAsync_TenantAdminDifferentRealm_ThrowsForbiddenOperationException()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");

        await Should.ThrowAsync<ForbiddenOperationException>(
            () => _sut.UpdateUserAsync(caller, "client-b", "user-1", new UpdateUserDto { Email = "x@client-b.com", Enabled = true }));
    }

    [Fact]
    public async Task UpdateUserAsync_SetsKeycloakUsernameToEmailAndReturnsRefreshedRoles()
    {
        var caller = new AdminCallerContext(IsPlatformAdmin: false, RealmSlug: "client-a");
        _userAccessor.GetUserByIdAsync("client-a", "user-1", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakUserInfo { Id = "user-1", Username = "updated@client-a.com", Email = "updated@client-a.com", Enabled = false });
        _userAccessor.GetUserRealmRolesAsync("client-a", "user-1", Arg.Any<CancellationToken>())
            .Returns(new List<KeyCloakRoleInfo> { new() { Id = "role-1", Name = "product-manager" } });

        var request = new UpdateUserDto { Email = "updated@client-a.com", Enabled = false };
        var result = await _sut.UpdateUserAsync(caller, "client-a", "user-1", request);

        await _userAccessor.Received(1).UpdateUserAsync(
            "client-a", "user-1",
            Arg.Is<UpdateKeyCloakUserRequest>(r => r.Username == "updated@client-a.com" && r.Email == "updated@client-a.com" && !r.Enabled),
            Arg.Any<CancellationToken>());
        result.Enabled.ShouldBeFalse();
        result.Roles.ShouldBe(["product-manager"]);
    }
}
