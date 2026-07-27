namespace OpenHub.SalesOrg.Contracts;

/// <summary>
/// Published by <c>OpenHub.SalesOrg.Api</c>'s <c>SalesAssignmentManager</c> on create/update/delete
/// — see <c>Events:SalesAssignment:ExchangeName</c>. Empty by default (no exchange configured)
/// since <c>OpenHub.SalesOrg.EventProcessor</c> is still a scaffold with nothing consuming yet.
/// </summary>
public sealed class SalesAssignmentChangedEvent
{
    public Guid SalesAssignmentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SalesPersonId { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public bool IsDeleted { get; set; }
}
