using FluentValidation;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Validators;

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(500)
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);

        RuleFor(x => x.ShortDescription)
            .MaximumLength(500)
            .When(x => x.ShortDescription is not null);

        RuleFor(x => x.Manufacturer)
            .MaximumLength(200)
            .When(x => x.Manufacturer is not null);

        RuleFor(x => x.Brand)
            .MaximumLength(200)
            .When(x => x.Brand is not null);

        RuleFor(x => x.ManufacturerPartNumber)
            .MaximumLength(200)
            .When(x => x.ManufacturerPartNumber is not null);

        RuleFor(x => x.Weight)
            .GreaterThan(0)
            .When(x => x.Weight.HasValue);
    }
}
