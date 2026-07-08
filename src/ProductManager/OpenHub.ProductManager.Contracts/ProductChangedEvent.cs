namespace OpenHub.ProductManager.Contracts;

public sealed class ProductChangedEvent
{
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Keycloak realm slug the change originated from (e.g. "client-a"). Lets
    /// downstream consumers request an M2M token for the right tenant realm
    /// without maintaining a separate tenant-to-realm lookup.
    /// </summary>
    public string RealmSlug { get; set; } = null!;

    public bool IsDeleted { get; set; } = false;
}
