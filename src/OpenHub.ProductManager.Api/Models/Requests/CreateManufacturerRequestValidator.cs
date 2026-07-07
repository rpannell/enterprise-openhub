using FluentValidation;

namespace OpenHub.ProductManager.Api.Models.Requests;

public class CreateManufacturerRequestValidator : AbstractValidator<CreateManufacturerRequest>
{
    public CreateManufacturerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
