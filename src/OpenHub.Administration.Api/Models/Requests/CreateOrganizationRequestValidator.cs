using FluentValidation;

namespace OpenHub.Administration.Api.Models.Requests;

public class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.RealmSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Alias).NotEmpty().MaximumLength(100);
    }
}
