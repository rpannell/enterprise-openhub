using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.Accessor.RealtimeMessaging.Models;

/// <summary>Wire shape posted to OpenHub.RealtimeMessaging.Api's <c>POST /api/v1/messages</c> —
/// mirrors that API's own <c>SendMessageRequest</c> field-for-field. Kept local rather than
/// referencing the Api project directly, since accessors depend on contracts, not on another
/// service's internal implementation types.</summary>
internal sealed record SendMessageRequestBody(
    MessageTarget Target,
    string? TenantSlug,
    string? UserId,
    string MessageType,
    string PayloadJson,
    string? CorrelationId);
