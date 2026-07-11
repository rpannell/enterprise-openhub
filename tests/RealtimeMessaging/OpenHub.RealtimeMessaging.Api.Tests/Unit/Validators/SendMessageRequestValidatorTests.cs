using OpenHub.RealtimeMessaging.Api.Models.Requests;
using OpenHub.RealtimeMessaging.Api.Validators;
using OpenHub.RealtimeMessaging.Contracts;
using Shouldly;
using Xunit;

namespace OpenHub.RealtimeMessaging.Api.Tests.Unit.Validators;

public class SendMessageRequestValidatorTests
{
    private readonly SendMessageRequestValidator _validator = new();

    private static SendMessageRequest MakeValid(MessageTarget target = MessageTarget.Tenant) => new()
    {
        Target = target,
        TenantSlug = target is MessageTarget.Tenant or MessageTarget.User ? "client-a" : null,
        UserId = target == MessageTarget.User ? "user-1" : null,
        MessageType = "test",
        PayloadJson = "{}",
    };

    [Theory]
    [InlineData(MessageTarget.Tenant)]
    [InlineData(MessageTarget.User)]
    [InlineData(MessageTarget.Broadcast)]
    public void Validate_ValidRequest_IsValid(MessageTarget target)
    {
        var result = _validator.Validate(MakeValid(target));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyMessageType_IsInvalid()
    {
        var request = MakeValid();
        request.MessageType = "";

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SendMessageRequest.MessageType));
    }

    [Fact]
    public void Validate_EmptyPayloadJson_IsInvalid()
    {
        var request = MakeValid();
        request.PayloadJson = "";

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SendMessageRequest.PayloadJson));
    }

    [Fact]
    public void Validate_TenantTarget_MissingTenantSlug_IsInvalid()
    {
        var request = MakeValid(MessageTarget.Tenant);
        request.TenantSlug = null;

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SendMessageRequest.TenantSlug));
    }

    [Fact]
    public void Validate_UserTarget_MissingUserId_IsInvalid()
    {
        var request = MakeValid(MessageTarget.User);
        request.UserId = null;

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SendMessageRequest.UserId));
    }

    [Fact]
    public void Validate_BroadcastTarget_NoTenantSlugRequired_IsValid()
    {
        var request = MakeValid(MessageTarget.Broadcast);

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
