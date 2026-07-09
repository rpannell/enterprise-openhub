namespace OpenHub.ProductManager.Api;

/// <summary>
/// Magic strings used across this API's composition root (Program.cs) and elsewhere, collected
/// here so they can't drift out of sync between call sites — e.g. a value used to build a cache
/// key in one accessor and independently duplicated as a literal somewhere else.
/// </summary>
public static class ApiConst
{
    /// <summary>
    /// Prefixes every key this service sends to the shared Redis instance (see Program.cs).
    /// product-manager-api and crm-api share one Aspire Redis resource — without a unique
    /// per-service prefix, identically-shaped cache keys (e.g. "tenant:id:{tenantId}") collide
    /// across services. Must stay unique across every API that shares that Redis instance.
    /// </summary>
    public const string RedisInstanceName = "ProductManager:";
}
