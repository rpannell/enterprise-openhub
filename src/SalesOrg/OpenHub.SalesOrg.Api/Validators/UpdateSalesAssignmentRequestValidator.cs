using FluentValidation;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Validators;

public sealed class UpdateSalesAssignmentRequestValidator : AbstractValidator<UpdateSalesAssignmentRequest>
{
    public UpdateSalesAssignmentRequestValidator()
    {
        // No field-shape rules — every field is independently optional patch data.
    }
}
