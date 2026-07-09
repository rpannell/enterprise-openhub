using FluentValidation;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Validators;

public sealed class CreateSalesAssignmentRequestValidator : AbstractValidator<CreateSalesAssignmentRequest>
{
    public CreateSalesAssignmentRequestValidator()
    {
        RuleFor(x => x.SalesPersonId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.EffectiveEndDate)
            .GreaterThanOrEqualTo(x => x.EffectiveStartDate)
            .When(x => x.EffectiveEndDate.HasValue)
            .WithMessage("EffectiveEndDate must be on or after EffectiveStartDate.");
    }
}
