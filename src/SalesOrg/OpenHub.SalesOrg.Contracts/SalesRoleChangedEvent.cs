namespace OpenHub.SalesOrg.Contracts;

/// <summary>
/// Published by <c>OpenHub.SalesOrg.Api</c>'s <c>SalesRoleManager</c> on create/update/delete —
/// see <c>Events:SalesRole:ExchangeName</c>. Empty by default (no exchange configured) since
/// <c>OpenHub.SalesOrg.EventProcessor</c> is still a scaffold with nothing consuming yet.
/// </summary>
public sealed class SalesRoleChangedEvent
{
    public Guid SalesRoleId { get; set; }
    public Guid TenantId { get; set; }
    public bool IsDeleted { get; set; }
}
