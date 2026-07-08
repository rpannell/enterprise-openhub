using FluentValidation;

namespace OpenHub.Administration.Api.Models.Requests;

public class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    public AssignRoleRequestValidator()
    {
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(100);
    }
}
