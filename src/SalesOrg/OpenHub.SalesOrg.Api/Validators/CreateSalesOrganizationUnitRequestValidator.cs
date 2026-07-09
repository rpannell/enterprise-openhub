using FluentValidation;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Validators;

public sealed class CreateSalesOrganizationUnitRequestValidator : AbstractValidator<CreateSalesOrganizationUnitRequest>
{
    public CreateSalesOrganizationUnitRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.OrganizationUnitType).NotEmpty().MaximumLength(100);
    }
}
