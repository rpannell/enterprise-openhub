using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenHub.Administration.Api.Auth;
using Shouldly;
using Xunit;

namespace OpenHub.Administration.Api.Tests.Unit.Auth;

public class TenantAdminHandlerTests
{
    private readonly TenantAdminHandler _sut = new();

    private static AuthorizationHandlerContext Context(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        return new AuthorizationHandlerContext([new TenantAdminRequirement()], principal, null);
    }

    [Fact]
    public async Task HandleRequirementAsync_TenantAdminRole_Succeeds()
    {
        var context = Context(new Claim(ClaimTypes.Role, "tenant-admin"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_PlatformAdminRole_Succeeds()
    {
        var context = Context(new Claim(ClaimTypes.Role, "platform-admin"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_NeitherRole_Fails()
    {
        var context = Context(new Claim(ClaimTypes.Role, "user"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }
}
