namespace OpenHub.CRM.Contracts;

public sealed class ClientChangedEvent
{
    public Guid ClientId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string RealmSlug { get; set; } = null!;
    public bool IsDeleted { get; set; } = false;
}
