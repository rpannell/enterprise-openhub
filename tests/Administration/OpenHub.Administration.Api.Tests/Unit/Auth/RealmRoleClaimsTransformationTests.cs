using System.Security.Claims;
using OpenHub.Administration.Api.Auth;
using Shouldly;
using Xunit;

namespace OpenHub.Administration.Api.Tests.Unit.Auth;

public class RealmRoleClaimsTransformationTests
{
    private readonly RealmRoleClaimsTransformation _sut = new();

    private static ClaimsPrincipal Principal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task TransformAsync_RealmAccessWithRoles_AddsRoleClaims()
    {
        var principal = Principal(new Claim("realm_access", """{"roles":["tenant-admin","user"]}"""));

        var result = await _sut.TransformAsync(principal);

        result.IsInRole("tenant-admin").ShouldBeTrue();
        result.IsInRole("user").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_NoRealmAccessClaim_NoOps()
    {
        var principal = Principal(new Claim("sub", "user-1"));

        var result = await _sut.TransformAsync(principal);

        result.Claims.Count(c => c.Type == ClaimTypes.Role).ShouldBe(0);
    }

    [Fact]
    public async Task TransformAsync_MalformedRealmAccessJson_DoesNotThrow_NoOps()
    {
        var principal = Principal(new Claim("realm_access", "{not valid json"));

        var result = await _sut.TransformAsync(principal);

        result.Claims.Count(c => c.Type == ClaimTypes.Role).ShouldBe(0);
    }

    [Fact]
    public async Task TransformAsync_CalledTwice_DoesNotDuplicateRoleClaims()
    {
        var principal = Principal(new Claim("realm_access", """{"roles":["tenant-admin"]}"""));

        await _sut.TransformAsync(principal);
        var result = await _sut.TransformAsync(principal);

        result.Claims.Count(c => c.Type == ClaimTypes.Role && c.Value == "tenant-admin").ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_RolesArrayEmpty_NoRoleClaimsAdded()
    {
        var principal = Principal(new Claim("realm_access", """{"roles":[]}"""));

        var result = await _sut.TransformAsync(principal);

        result.Claims.Count(c => c.Type == ClaimTypes.Role).ShouldBe(0);
    }
}
