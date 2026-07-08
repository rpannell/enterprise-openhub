using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Tests.Unit.Validators;

public class SyncUserRequestValidatorTests
{
    private readonly SyncUserRequestValidator _validator = new();

    private static SyncUserRequest MakeValid() => new()
    {
        TenantId = Guid.NewGuid(),
        UniqueId = "kc-user-1",
        Email = "a@example.com",
        FirstName = "Ada",
        LastName = "Lovelace",
        IsDeleted = false,
    };

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var result = _validator.Validate(MakeValid());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyTenantId_IsInvalid()
    {
        var request = MakeValid();
        request.TenantId = Guid.Empty;

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncUserRequest.TenantId));
    }

    [Fact]
    public void Validate_EmptyUniqueId_IsInvalid()
    {
        var request = MakeValid();
        request.UniqueId = "";

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncUserRequest.UniqueId));
    }

    [Fact]
    public void Validate_UniqueIdOverMaxLength_IsInvalid()
    {
        var request = MakeValid();
        request.UniqueId = new string('a', 201);

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncUserRequest.UniqueId));
    }

    [Fact]
    public void Validate_EmailOverMaxLength_IsInvalid()
    {
        var request = MakeValid();
        request.Email = new string('a', 316) + "@a.com"; // > 320 chars

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncUserRequest.Email));
    }

    [Fact]
    public void Validate_FirstNameOverMaxLength_IsInvalid()
    {
        var request = MakeValid();
        request.FirstName = new string('a', 201);

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncUserRequest.FirstName));
    }

    [Fact]
    public void Validate_LastNameOverMaxLength_IsInvalid()
    {
        var request = MakeValid();
        request.LastName = new string('a', 201);

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncUserRequest.LastName));
    }

    [Fact]
    public void Validate_AllOptionalFieldsNull_ConditionalRulesDoNotFire_IsValid()
    {
        var request = MakeValid();
        request.Email = null;
        request.FirstName = null;
        request.LastName = null;

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
