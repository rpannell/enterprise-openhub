using OpenHub.RealtimeMessaging.Api.Managers.Engines;
using OpenHub.RealtimeMessaging.Api.Managers.Models;
using Shouldly;
using Xunit;

namespace OpenHub.RealtimeMessaging.Api.Tests.Unit.Managers;

public class MessageTargetAuthorizationEngineTests
{
    private readonly MessageTargetAuthorizationEngine _engine = new();

    [Fact]
    public void Authorize_SameRealm_Allows()
    {
        var result = _engine.Authorize(new CallerContextDto("client-a", false), "client-a");

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public void Authorize_SameRealm_CaseInsensitive_Allows()
    {
        var result = _engine.Authorize(new CallerContextDto("Client-A", false), "client-a");

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public void Authorize_DifferentRealm_TenantCaller_Denies()
    {
        var result = _engine.Authorize(new CallerContextDto("client-a", false), "client-b");

        result.Authorized.ShouldBeFalse();
        result.Reason.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Authorize_DifferentRealm_PlatformAdminCaller_Allows()
    {
        var result = _engine.Authorize(new CallerContextDto("platform-admin", true), "client-b");

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public void Authorize_CallerHasNoRealmSlug_Denies()
    {
        var result = _engine.Authorize(new CallerContextDto(null, false), "client-a");

        result.Authorized.ShouldBeFalse();
    }
}
