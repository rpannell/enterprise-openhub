using FluentValidation;

namespace OpenHub.Administration.Api.Models.Requests;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(300);
        RuleFor(x => x.FirstName).MaximumLength(200);
        RuleFor(x => x.LastName).MaximumLength(200);
    }
}
