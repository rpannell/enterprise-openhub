using FluentValidation;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Validators;

public sealed class UpdateSalesPersonRequestValidator : AbstractValidator<UpdateSalesPersonRequest>
{
    public UpdateSalesPersonRequestValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(200).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName).MaximumLength(200).When(x => x.LastName is not null);
        RuleFor(x => x.Phone).MaximumLength(30).When(x => x.Phone is not null);
        RuleFor(x => x.JobTitle).MaximumLength(200).When(x => x.JobTitle is not null);
        RuleFor(x => x.EmployeeNumber).MaximumLength(50).When(x => x.EmployeeNumber is not null);
    }
}
