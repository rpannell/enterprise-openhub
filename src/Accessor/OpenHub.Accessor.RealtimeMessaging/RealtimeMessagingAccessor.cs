using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OpenHub.Accessor.RealtimeMessaging.Interfaces;
using OpenHub.Accessor.RealtimeMessaging.Models;
using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.Accessor.RealtimeMessaging;

public class RealtimeMessagingAccessor(HttpClient http) : IRealtimeMessagingAccessor
{
    // ASP.NET Core's own Web defaults (camelCase, case-insensitive) — payloads are typically
    // read by TypeScript on the frontend, so this keeps casing consistent with everything else
    // the platform's APIs already emit.
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    public Task SendToTenantAsync(
        string tenantSlug, string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        SendToTenantAsync(
            tenantSlug, messageType, JsonSerializer.Serialize(payload, PayloadSerializerOptions), accessToken,
            correlationId, ct);

    public Task SendToTenantAsync(
        string tenantSlug, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        SendAsync(
            new SendMessageRequestBody(MessageTarget.Tenant, tenantSlug, null, messageType, payloadJson, correlationId),
            accessToken, ct);

    public Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        SendToUserAsync(
            tenantSlug, userId, messageType, JsonSerializer.Serialize(payload, PayloadSerializerOptions), accessToken,
            correlationId, ct);

    public Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        SendAsync(
            new SendMessageRequestBody(MessageTarget.User, tenantSlug, userId, messageType, payloadJson, correlationId),
            accessToken, ct);

    public Task SendToAllAsync(
        string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        SendToAllAsync(
            messageType, JsonSerializer.Serialize(payload, PayloadSerializerOptions), accessToken, correlationId, ct);

    public Task SendToAllAsync(
        string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        SendAsync(
            new SendMessageRequestBody(MessageTarget.Broadcast, null, null, messageType, payloadJson, correlationId),
            accessToken, ct);

    private async Task SendAsync(SendMessageRequestBody body, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/messages")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
