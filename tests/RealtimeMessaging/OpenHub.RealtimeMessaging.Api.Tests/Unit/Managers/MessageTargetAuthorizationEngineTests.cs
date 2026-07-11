using OpenHub.RealtimeMessaging.Api.Managers.Engines;
using OpenHub.RealtimeMessaging.Api.Managers.Models;
using OpenHub.RealtimeMessaging.Contracts;
using Shouldly;
using Xunit;

namespace OpenHub.RealtimeMessaging.Api.Tests.Unit.Managers;

public class MessageTargetAuthorizationEngineTests
{
    private readonly MessageTargetAuthorizationEngine _engine = new();

    private static SendMessageDto TenantMessage(string? tenantSlug) => new(
        MessageTarget.Tenant, tenantSlug, null, "test", "{}", null);

    private static SendMessageDto UserMessage(string? tenantSlug) => new(
        MessageTarget.User, tenantSlug, "user-1", "test", "{}", null);

    private static SendMessageDto BroadcastMessage() => new(
        MessageTarget.Broadcast, null, null, "test", "{}", null);

    [Fact]
    public void Authorize_Broadcast_PlatformAdminCaller_Allows()
    {
        var result = _engine.Authorize(new CallerContextDto("platform-admin", true), BroadcastMessage());

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public void Authorize_Broadcast_TenantCaller_Denies()
    {
        var result = _engine.Authorize(new CallerContextDto("client-a", false), BroadcastMessage());

        result.Authorized.ShouldBeFalse();
        result.Reason.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Authorize_Tenant_SameRealm_Allows()
    {
        var result = _engine.Authorize(new CallerContextDto("client-a", false), TenantMessage("client-a"));

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public void Authorize_Tenant_SameRealm_CaseInsensitive_Allows()
    {
        var result = _engine.Authorize(new CallerContextDto("Client-A", false), TenantMessage("client-a"));

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public void Authorize_Tenant_DifferentRealm_TenantCaller_Denies()
    {
        var result = _engine.Authorize(new CallerContextDto("client-a", false), TenantMessage("client-b"));

        result.Authorized.ShouldBeFalse();
    }

    [Fact]
    public void Authorize_Tenant_DifferentRealm_PlatformAdminCaller_Allows()
    {
        var result = _engine.Authorize(new CallerContextDto("platform-admin", true), TenantMessage("client-b"));

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public void Authorize_User_SameRealm_Allows()
    {
        var result = _engine.Authorize(new CallerContextDto("client-a", false), UserMessage("client-a"));

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public void Authorize_User_DifferentRealm_TenantCaller_Denies()
    {
        var result = _engine.Authorize(new CallerContextDto("client-a", false), UserMessage("client-b"));

        result.Authorized.ShouldBeFalse();
    }

    [Fact]
    public void Authorize_Tenant_CallerHasNoRealmSlug_Denies()
    {
        var result = _engine.Authorize(new CallerContextDto(null, false), TenantMessage("client-a"));

        result.Authorized.ShouldBeFalse();
    }
}
