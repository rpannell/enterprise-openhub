namespace OpenHub.SalesOrg.Contracts;

/// <summary>
/// Not published yet — OpenHub.SalesOrg.EventProcessor is a scaffold with no messaging wired up.
/// Defined now so downstream consumers (e.g. a future CRM sync) know the intended shape once
/// SalesAssignment create/move/close starts publishing.
/// </summary>
public sealed class SalesAssignmentChangedEvent
{
    public Guid SalesAssignmentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SalesPersonId { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public bool IsDeleted { get; set; }
}
