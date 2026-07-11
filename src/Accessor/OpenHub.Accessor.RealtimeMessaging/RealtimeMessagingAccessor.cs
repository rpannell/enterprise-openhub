using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenHub.Accessor.RealtimeMessaging.Interfaces;
using OpenHub.Accessor.RealtimeMessaging.Models;
using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.Accessor.RealtimeMessaging;

public class RealtimeMessagingAccessor(HttpClient http) : IRealtimeMessagingAccessor
{
    public Task SendToTenantAsync(
        string tenantSlug, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        SendAsync(
            new SendMessageRequestBody(MessageTarget.Tenant, tenantSlug, null, messageType, payloadJson, correlationId),
            accessToken, ct);

    public Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        SendAsync(
            new SendMessageRequestBody(MessageTarget.User, tenantSlug, userId, messageType, payloadJson, correlationId),
            accessToken, ct);

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
