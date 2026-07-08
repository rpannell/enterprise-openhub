using FluentValidation;

namespace OpenHub.ProductManager.Api.Models.Requests;

public class SyncUserRequestValidator : AbstractValidator<SyncUserRequest>
{
    public SyncUserRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UniqueId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(320).When(x => x.Email is not null);
        RuleFor(x => x.FirstName).MaximumLength(200).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName).MaximumLength(200).When(x => x.LastName is not null);
    }
}
