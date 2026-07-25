namespace OpenHub.SalesOrg.Contracts;

/// <summary>
/// Published by <c>OpenHub.SalesOrg.Api</c>'s <c>SalesOrganizationUnitManager</c> on
/// create/update/delete — see <c>Events:SalesOrganizationUnit:ExchangeName</c>. Empty by default
/// (no exchange configured) since <c>OpenHub.SalesOrg.EventProcessor</c> is still a scaffold with
/// nothing consuming yet.
/// </summary>
public sealed class SalesOrganizationUnitChangedEvent
{
    public Guid SalesOrganizationUnitId { get; set; }
    public Guid TenantId { get; set; }
    public bool IsDeleted { get; set; }
}
