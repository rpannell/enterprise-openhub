using Shouldly;
using Xunit;
using OpenHub.CRM.Api.Models.Requests;
using OpenHub.CRM.Api.Validators;

namespace OpenHub.CRM.Api.Tests.Unit.Validators;

public class CreateClientRequestValidatorTests
{
    private readonly CreateClientRequestValidator _validator = new();

    private static CreateClientRequest MakeValidRequest() => new()
    {
        Name = "Acme Co",
        SalesPersonId = Guid.NewGuid(),
        Status = "prospect",
        AccountTier = "standard",
        Addresses = [],
        Contacts = [],
    };

    [Fact]
    public void Valid_HappyPath_PassesValidation()
    {
        var result = _validator.Validate(MakeValidRequest());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyName_FailsValidation()
    {
        var request = MakeValidRequest();
        request.Name = "";

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void EmptySalesPersonIdRule_FailsValidation()
    {
        var request = MakeValidRequest();
        request.SalesPersonId = Guid.Empty;

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void InvalidStatus_FailsValidation()
    {
        var request = MakeValidRequest();
        request.Status = "not-a-real-status";

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void InvalidAccountTier_FailsValidation()
    {
        var request = MakeValidRequest();
        request.AccountTier = "not-a-real-tier";

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AddressWithInvalidCountryCode_FailsValidation()
    {
        var request = MakeValidRequest();
        request.Addresses = [new ClientAddressRequest { Line1 = "1 Main St", City = "Anytown", CountryCode = "USA" }];

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ContactWithInvalidEmail_FailsValidation()
    {
        var request = MakeValidRequest();
        request.Contacts = [new ClientContactRequest { FirstName = "Jane", LastName = "Doe", Email = "not-an-email" }];

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }
}
