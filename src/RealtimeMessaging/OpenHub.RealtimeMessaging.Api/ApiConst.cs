namespace OpenHub.RealtimeMessaging.Api;

/// <summary>
/// Magic strings used across this API's composition root (Program.cs) and elsewhere, collected
/// here so they can't drift out of sync between call sites.
/// </summary>
public static class ApiConst
{
    /// <summary>
    /// Prefixes every Redis pub/sub channel SignalR's backplane uses on the shared Aspire Redis
    /// instance — nothing else publishes on that instance today, but this avoids a future
    /// collision the same way each API's HybridCache RedisInstanceName prefix does.
    /// </summary>
    public const string RedisChannelPrefix = "realtime-messaging";

    /// <summary>
    /// Route the SignalR hub is mapped at. Shared between Program.cs (MapHub) and the
    /// JWT-bearer query-string-token workaround, which must only apply to requests targeting
    /// this exact path — every other endpoint on this API still requires a real
    /// Authorization header.
    /// </summary>
    public const string HubRoute = "/hubs/notifications";
}
