using OpenHub.SalesOrg.Api.Managers.Models;

namespace OpenHub.SalesOrg.Api.Managers.Engines.Interfaces;

// Pure logic only — all data arrives as method parameters. No Accessor/Manager dependencies.
public interface ISalesOrgHierarchyEngine
{
    /// <summary>
    /// Walks from a sales person up through their unit's in-unit outrankers and then
    /// successive parent units' top-ranked assignee, nearest link first. Stops at the first
    /// unit with no parent, or when a cycle in the org-unit graph is detected.
    /// </summary>
    IReadOnlyList<Guid> GetManagerChain(
        IReadOnlyList<SalesAssignmentDto> assignments,
        IReadOnlyList<SalesRoleDto> roles,
        IReadOnlyList<SalesOrganizationUnitDto> units,
        Guid salesPersonId);

    /// <summary>
    /// Same-unit peers with a lower role hierarchy level, plus — only if the person is their
    /// unit's top-ranked active+primary assignee — everyone assigned anywhere in the unit's
    /// entire descendant subtree.
    /// </summary>
    IReadOnlyList<Guid> GetReports(
        IReadOnlyList<SalesAssignmentDto> assignments,
        IReadOnlyList<SalesRoleDto> roles,
        IReadOnlyList<SalesOrganizationUnitDto> units,
        Guid salesPersonId);

    IReadOnlyList<Guid> GetDescendantOrgUnitIds(IReadOnlyList<SalesOrganizationUnitDto> allUnits, Guid unitId);

    IReadOnlyList<Guid> GetAncestorOrgUnitIds(IReadOnlyList<SalesOrganizationUnitDto> allUnits, Guid unitId);
}
