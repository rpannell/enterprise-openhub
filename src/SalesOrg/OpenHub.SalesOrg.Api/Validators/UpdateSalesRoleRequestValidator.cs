using FluentValidation;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Validators;

public sealed class UpdateSalesRoleRequestValidator : AbstractValidator<UpdateSalesRoleRequest>
{
    public UpdateSalesRoleRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
    }
}
