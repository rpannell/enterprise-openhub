using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Managers.Models;

public sealed record SendMessageDto(
    MessageTarget Target,
    string? TenantSlug,
    string? UserId,
    string MessageType,
    string PayloadJson,
    string? CorrelationId);
