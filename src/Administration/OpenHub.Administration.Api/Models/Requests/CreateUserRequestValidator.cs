using FluentValidation;

namespace OpenHub.Administration.Api.Models.Requests;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(300);
        RuleFor(x => x.FirstName).MaximumLength(200);
        RuleFor(x => x.LastName).MaximumLength(200);
        RuleForEach(x => x.InitialRoles).NotEmpty().MaximumLength(100);
    }
}
