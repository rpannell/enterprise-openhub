using FluentValidation;
using OpenHub.CRM.Api.Models.Requests;

namespace OpenHub.CRM.Api.Validators;

public sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    private static readonly string[] ValidStatuses = ["prospect", "active", "on_hold", "churned"];
    private static readonly string[] ValidTiers = ["strategic", "key", "standard", "small"];

    public UpdateClientRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(500).When(x => x.Name is not null);
        RuleFor(x => x.LegalName).MaximumLength(500).When(x => x.LegalName is not null);
        RuleFor(x => x.Status).Must(s => ValidStatuses.Contains(s))
            .When(x => x.Status is not null)
            .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}.");
        RuleFor(x => x.AccountTier).Must(t => ValidTiers.Contains(t))
            .When(x => x.AccountTier is not null)
            .WithMessage($"AccountTier must be one of: {string.Join(", ", ValidTiers)}.");
        RuleFor(x => x.AnnualRevenue).GreaterThanOrEqualTo(0).When(x => x.AnnualRevenue.HasValue);
        RuleFor(x => x.EmployeeCount).GreaterThanOrEqualTo(0).When(x => x.EmployeeCount.HasValue);

        RuleForEach(x => x.Addresses).SetValidator(new ClientAddressRequestValidator())
            .When(x => x.Addresses is not null);
        RuleForEach(x => x.Contacts).SetValidator(new ClientContactRequestValidator())
            .When(x => x.Contacts is not null);
    }
}
