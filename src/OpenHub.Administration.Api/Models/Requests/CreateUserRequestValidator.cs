using FluentValidation;

namespace OpenHub.Administration.Api.Models.Requests;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(300);
        RuleFor(x => x.FirstName).MaximumLength(200);
        RuleFor(x => x.LastName).MaximumLength(200);
        RuleFor(x => x.InitialRole).MaximumLength(100);
    }
}
