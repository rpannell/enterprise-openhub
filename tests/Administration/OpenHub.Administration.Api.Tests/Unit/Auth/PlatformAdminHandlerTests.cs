using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenHub.Administration.Api.Auth;
using Shouldly;
using Xunit;

namespace OpenHub.Administration.Api.Tests.Unit.Auth;

public class PlatformAdminHandlerTests
{
    private readonly PlatformAdminHandler _sut = new();

    private static AuthorizationHandlerContext Context(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        return new AuthorizationHandlerContext([new PlatformAdminRequirement()], principal, null);
    }

    [Fact]
    public async Task HandleRequirementAsync_RoleAndCorrectIssuer_Succeeds()
    {
        var context = Context(
            new Claim(ClaimTypes.Role, "platform-admin"),
            new Claim("iss", "https://keycloak.test/realms/platform-admin"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_RoleButWrongIssuer_Fails()
    {
        var context = Context(
            new Claim(ClaimTypes.Role, "platform-admin"),
            new Claim("iss", "https://keycloak.test/realms/client-a"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_CorrectIssuerButNoRole_Fails()
    {
        var context = Context(new Claim("iss", "https://keycloak.test/realms/platform-admin"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoClaimsAtAll_Fails()
    {
        var context = Context();

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }
}
