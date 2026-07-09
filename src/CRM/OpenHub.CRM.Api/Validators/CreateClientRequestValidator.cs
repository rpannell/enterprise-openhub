using FluentValidation;
using OpenHub.CRM.Api.Models.Requests;

namespace OpenHub.CRM.Api.Validators;

public sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    private static readonly string[] ValidStatuses = ["prospect", "active", "on_hold", "churned"];
    private static readonly string[] ValidTiers = ["strategic", "key", "standard", "small"];

    public CreateClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(500);
        RuleFor(x => x.LegalName).MaximumLength(500).When(x => x.LegalName is not null);
        RuleFor(x => x.SalesPersonId).NotEmpty();
        RuleFor(x => x.Status).Must(s => ValidStatuses.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}.");
        RuleFor(x => x.AccountTier).Must(t => ValidTiers.Contains(t))
            .WithMessage($"AccountTier must be one of: {string.Join(", ", ValidTiers)}.");
        RuleFor(x => x.AnnualRevenue).GreaterThanOrEqualTo(0).When(x => x.AnnualRevenue.HasValue);
        RuleFor(x => x.EmployeeCount).GreaterThanOrEqualTo(0).When(x => x.EmployeeCount.HasValue);

        RuleForEach(x => x.Addresses).SetValidator(new ClientAddressRequestValidator());
        RuleForEach(x => x.Contacts).SetValidator(new ClientContactRequestValidator());
    }
}

public sealed class ClientAddressRequestValidator : AbstractValidator<ClientAddressRequest>
{
    private static readonly string[] ValidTypes = ["billing", "shipping", "headquarters", "other"];

    public ClientAddressRequestValidator()
    {
        RuleFor(x => x.AddressType).Must(t => ValidTypes.Contains(t))
            .WithMessage($"AddressType must be one of: {string.Join(", ", ValidTypes)}.");
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Line2).MaximumLength(500).When(x => x.Line2 is not null);
        RuleFor(x => x.City).NotEmpty().MaximumLength(200);
        // StateProvince and PostalCode are deliberately unconstrained beyond length — not every
        // country structures addresses with a state/province (e.g. many single-region countries),
        // and postal code formats vary too widely internationally (numeric, alphanumeric, or
        // absent entirely) to validate a single shape.
        RuleFor(x => x.StateProvince).MaximumLength(200).When(x => x.StateProvince is not null);
        RuleFor(x => x.PostalCode).MaximumLength(20).When(x => x.PostalCode is not null);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
    }
}

public sealed class ClientContactRequestValidator : AbstractValidator<ClientContactRequest>
{
    public ClientContactRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleForEach(x => x.Phones).SetValidator(new ContactPhoneRequestValidator());
    }
}

public sealed class ContactPhoneRequestValidator : AbstractValidator<ContactPhoneRequest>
{
    private static readonly string[] ValidTypes = ["work", "cell", "home", "other"];

    public ContactPhoneRequestValidator()
    {
        RuleFor(x => x.PhoneType).Must(t => ValidTypes.Contains(t))
            .WithMessage($"PhoneType must be one of: {string.Join(", ", ValidTypes)}.");
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(30);
    }
}
