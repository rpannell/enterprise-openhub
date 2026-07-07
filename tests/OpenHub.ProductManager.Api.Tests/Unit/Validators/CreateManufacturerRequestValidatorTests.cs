using FluentValidation.TestHelper;
using OpenHub.ProductManager.Api.Models.Requests;
using Shouldly;
using Xunit;

namespace OpenHub.ProductManager.Api.Tests.Unit.Validators;

public class CreateManufacturerRequestValidatorTests
{
    private readonly CreateManufacturerRequestValidator _validator = new();

    [Fact]
    public void Validate_EmptyName_HasValidationError()
    {
        var request = new CreateManufacturerRequest { Name = string.Empty };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameExceedsMaximumLength_HasValidationError()
    {
        var request = new CreateManufacturerRequest { Name = new string('a', 201) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ValidName_HasNoValidationError()
    {
        var request = new CreateManufacturerRequest { Name = "Acme" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameAtMaximumLength_HasNoValidationError()
    {
        var request = new CreateManufacturerRequest { Name = new string('a', 200) };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
        result.IsValid.ShouldBeTrue();
    }
}
