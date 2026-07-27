using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using OpenHub.Accessor.RealtimeMessaging.Interfaces;

namespace OpenHub.Accessor.RealtimeMessaging;

public class RealtimeMessagingAccessor(IConfiguration configuration) : IRealtimeMessagingAccessor
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
        InvokeAsync("SendToTenant", accessToken, ct, tenantSlug, messageType, payloadJson, correlationId);

    public Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        SendToUserAsync(
            tenantSlug, userId, messageType, JsonSerializer.Serialize(payload, PayloadSerializerOptions), accessToken,
            correlationId, ct);

    public Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        InvokeAsync("SendToUser", accessToken, ct, tenantSlug, userId, messageType, payloadJson, correlationId);

    public Task BroadcastAsync(
        string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        BroadcastAsync(
            messageType, JsonSerializer.Serialize(payload, PayloadSerializerOptions), accessToken, correlationId, ct);

    public Task BroadcastAsync(
        string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default) =>
        InvokeAsync("Broadcast", accessToken, ct, messageType, payloadJson, correlationId);

    private async Task InvokeAsync(string method, string accessToken, CancellationToken ct, params object?[] args)
    {
        var hubUrl = configuration["RealtimeMessagingApi:HubUrl"] ?? "https+http://realtime-messaging-api/hubs/notifications";

        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options => options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken))
            .Build();

        await connection.StartAsync(ct);
        await connection.InvokeCoreAsync(method, args, ct);
    }
}
