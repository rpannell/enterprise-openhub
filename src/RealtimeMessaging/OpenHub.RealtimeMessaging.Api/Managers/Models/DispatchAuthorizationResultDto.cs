namespace OpenHub.RealtimeMessaging.Api.Managers.Models;

public sealed record DispatchAuthorizationResultDto(bool Authorized, string? Reason)
{
    public static DispatchAuthorizationResultDto Allow() => new(true, null);

    public static DispatchAuthorizationResultDto Deny(string reason) => new(false, reason);
}
