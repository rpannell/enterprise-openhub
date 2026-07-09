using FluentValidation;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Validators;

public sealed class UpdateSalesOrganizationUnitRequestValidator : AbstractValidator<UpdateSalesOrganizationUnitRequest>
{
    public UpdateSalesOrganizationUnitRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.OrganizationUnitType).MaximumLength(100).When(x => x.OrganizationUnitType is not null);
    }
}
