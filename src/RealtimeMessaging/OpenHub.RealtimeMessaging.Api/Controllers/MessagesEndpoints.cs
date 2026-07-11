using System.Security.Claims;
using Asp.Versioning;
using OpenHub.RealtimeMessaging.Api.Auth;
using OpenHub.RealtimeMessaging.Api.Managers.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers.Models;
using OpenHub.RealtimeMessaging.Api.Mappers;
using OpenHub.RealtimeMessaging.Api.Models.Requests;
using OpenHub.RealtimeMessaging.Api.Models.Responses.V1;

namespace OpenHub.RealtimeMessaging.Api.Controllers;

/// <summary>
/// M2M-only publish endpoint — gated by the "RealtimeMessagingPublisher" scope-claim policy, so
/// interactive user tokens (issued to front-end clients connecting to NotificationHub) can never
/// reach it. Target-scope rules (own-tenant vs. any-tenant vs. broadcast) are enforced by
/// IMessageDispatchManager/IMessageTargetAuthorizationEngine, not here.
/// </summary>
public static class MessagesEndpoints
{
    public static IEndpointRouteBuilder MapMessagesEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/messages")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization("RealtimeMessagingPublisher")
            .WithTags("Messages");

        group.MapPost("/", async (
            SendMessageRequest request,
            ClaimsPrincipal user,
            IMessageDispatchManager manager,
            MessageMapper mapper,
            CancellationToken ct) =>
        {
            var caller = new CallerContextDto(user.GetRealmSlug(), user.IsPlatformAdmin());
            var result = await manager.DispatchAsync(caller, mapper.ToDto(request), ct);

            return result.Authorized
                ? Results.Accepted(value: new SendMessageAcceptedResponse
                {
                    CorrelationId = request.CorrelationId,
                    SentAtUtc = DateTimeOffset.UtcNow
                })
                : Results.Forbid();
        })
        .WithName("SendMessage")
        .WithSummary("Send a real-time message")
        .WithDescription("Fire-and-forget push over SignalR. Tenant-scoped M2M callers may only target their " +
            "own realm/tenant or a user within it. Only platform-admin M2M callers (platform-admin realm) may " +
            "target Broadcast or an arbitrary tenant/user outside their own realm. No message history/persistence.")
        .Produces<SendMessageAcceptedResponse>(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter<SendMessageRequest>>();

        return app;
    }
}
