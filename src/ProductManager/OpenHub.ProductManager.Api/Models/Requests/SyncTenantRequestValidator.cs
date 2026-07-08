using FluentValidation;

namespace OpenHub.ProductManager.Api.Models.Requests;

public class SyncTenantRequestValidator : AbstractValidator<SyncTenantRequest>
{
    public SyncTenantRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.RealmSlug).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Name).MaximumLength(500).When(x => x.Name is not null);
    }
}
