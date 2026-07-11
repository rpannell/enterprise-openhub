using FluentValidation;
using OpenHub.RealtimeMessaging.Api.Models.Requests;
using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Validators;

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.MessageType).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PayloadJson).NotEmpty();

        When(x => x.Target is MessageTarget.Tenant or MessageTarget.User, () =>
        {
            RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(200);
        });

        When(x => x.Target == MessageTarget.User, () =>
        {
            RuleFor(x => x.UserId).NotEmpty();
        });
    }
}
