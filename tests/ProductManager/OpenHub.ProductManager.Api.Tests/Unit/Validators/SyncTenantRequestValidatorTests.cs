using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Tests.Unit.Validators;

public class SyncTenantRequestValidatorTests
{
    private readonly SyncTenantRequestValidator _validator = new();

    private static SyncTenantRequest MakeValid() => new()
    {
        TenantId = Guid.NewGuid(),
        RealmSlug = "client-a",
        Name = "Client A",
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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncTenantRequest.TenantId));
    }

    [Fact]
    public void Validate_EmptyRealmSlug_IsInvalid()
    {
        var request = MakeValid();
        request.RealmSlug = "";

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncTenantRequest.RealmSlug));
    }

    [Fact]
    public void Validate_RealmSlugOverMaxLength_IsInvalid()
    {
        var request = MakeValid();
        request.RealmSlug = new string('a', 201);

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncTenantRequest.RealmSlug));
    }

    [Fact]
    public void Validate_NameOverMaxLength_IsInvalid()
    {
        var request = MakeValid();
        request.Name = new string('a', 501);

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SyncTenantRequest.Name));
    }

    [Fact]
    public void Validate_NameNull_ConditionalRuleDoesNotFire_IsValid()
    {
        var request = MakeValid();
        request.Name = null;

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_IsDeletedTrue_NameNull_StillValid()
    {
        var request = MakeValid();
        request.IsDeleted = true;
        request.Name = null;

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
