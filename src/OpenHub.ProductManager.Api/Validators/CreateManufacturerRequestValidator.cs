using FluentValidation;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Validators;

public sealed class CreateManufacturerRequestValidator : AbstractValidator<CreateManufacturerRequest>
{
    public CreateManufacturerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}
