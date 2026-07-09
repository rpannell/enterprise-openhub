using OpenHub.SalesOrg.Api.Managers.Engines.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Models;

namespace OpenHub.SalesOrg.Api.Managers.Engines;

// Pure logic only — all data arrives as method parameters. No Accessor/Manager dependencies.
//
// Hierarchy is derived from org units + role hierarchy level, not a stored manager pointer:
// within any org unit, whoever holds the active, primary assignment with the highest
// SalesRole.HierarchyLevel is that unit's "lead."
public class SalesOrgHierarchyEngine : ISalesOrgHierarchyEngine
{
    private const int UnrankedLevel = int.MinValue;

    public IReadOnlyList<Guid> GetManagerChain(
        IReadOnlyList<SalesAssignmentDto> assignments,
        IReadOnlyList<SalesRoleDto> roles,
        IReadOnlyList<SalesOrganizationUnitDto> units,
        Guid salesPersonId)
    {
        var roleLevelById = BuildRoleLevelLookup(roles);
        var unitById = units.ToDictionary(u => u.Id);
        var activePrimaryByUnit = BuildActivePrimaryByUnit(assignments);

        var chain = new List<Guid>();

        var own = assignments.FirstOrDefault(a =>
            a.SalesPersonId == salesPersonId && a.ActiveStatus && a.PrimaryAssignmentFlag);
        if (own is null) return chain;

        var currentUnitId = own.OrganizationUnitId;
        var currentPersonId = salesPersonId;
        var currentLevel = roleLevelById.GetValueOrDefault(own.RoleId, UnrankedLevel);

        var visitedUnits = new HashSet<Guid> { currentUnitId };

        while (true)
        {
            // Keep checking for further in-unit outrankers of whoever is currently "it" —
            // rare but possible: two stacked roles in one unit.
            bool foundOutranker;
            do
            {
                foundOutranker = false;
                if (activePrimaryByUnit.TryGetValue(currentUnitId, out var peers))
                {
                    // Nearest outranker first (smallest level strictly above current), not the
                    // top-ranked one directly — otherwise a rep -> mid -> top chain in one unit
                    // would jump straight from rep to top and skip mid.
                    var outranker = peers
                        .Where(p => p.SalesPersonId != currentPersonId)
                        .Select(p => (p.SalesPersonId, Level: roleLevelById.GetValueOrDefault(p.RoleId, UnrankedLevel)))
                        .Where(p => p.Level > currentLevel)
                        .OrderBy(p => p.Level)
                        .Cast<(Guid SalesPersonId, int Level)?>()
                        .FirstOrDefault();

                    if (outranker is not null)
                    {
                        chain.Add(outranker.Value.SalesPersonId);
                        currentPersonId = outranker.Value.SalesPersonId;
                        currentLevel = outranker.Value.Level;
                        foundOutranker = true;
                    }
                }
            } while (foundOutranker);

            // No one left in-unit outranks the current top — climb to the parent unit.
            if (!unitById.TryGetValue(currentUnitId, out var unit) || unit.ParentOrganizationUnitId is null)
                break;

            var parentUnitId = unit.ParentOrganizationUnitId.Value;
            if (!visitedUnits.Add(parentUnitId))
                break; // cycle guard — a bad reparent edit shouldn't infinite-loop this

            currentUnitId = parentUnitId;

            if (!activePrimaryByUnit.TryGetValue(parentUnitId, out var parentPeers) || parentPeers.Count == 0)
                continue; // nobody leads the parent unit yet — keep climbing past it

            var top = parentPeers
                .Select(p => (p.SalesPersonId, Level: roleLevelById.GetValueOrDefault(p.RoleId, UnrankedLevel)))
                .OrderByDescending(p => p.Level)
                .First();

            chain.Add(top.SalesPersonId);
            currentPersonId = top.SalesPersonId;
            currentLevel = top.Level;
        }

        return chain;
    }

    public IReadOnlyList<Guid> GetReports(
        IReadOnlyList<SalesAssignmentDto> assignments,
        IReadOnlyList<SalesRoleDto> roles,
        IReadOnlyList<SalesOrganizationUnitDto> units,
        Guid salesPersonId)
    {
        var roleLevelById = BuildRoleLevelLookup(roles);
        var activeAssignments = assignments.Where(a => a.ActiveStatus && a.PrimaryAssignmentFlag).ToList();

        var own = activeAssignments.FirstOrDefault(a => a.SalesPersonId == salesPersonId);
        if (own is null) return [];

        var ownLevel = roleLevelById.GetValueOrDefault(own.RoleId, UnrankedLevel);
        var unitId = own.OrganizationUnitId;

        var peers = activeAssignments
            .Where(a => a.OrganizationUnitId == unitId && a.SalesPersonId != salesPersonId)
            .Select(a => (a.SalesPersonId, Level: roleLevelById.GetValueOrDefault(a.RoleId, UnrankedLevel)))
            .ToList();

        var result = new List<Guid>();
        result.AddRange(peers.Where(p => p.Level < ownLevel).Select(p => p.SalesPersonId));

        var isTopOfUnit = peers.All(p => p.Level <= ownLevel);
        if (isTopOfUnit)
        {
            var descendantUnitIds = GetDescendantOrgUnitIds(units, unitId).ToHashSet();
            result.AddRange(activeAssignments
                .Where(a => descendantUnitIds.Contains(a.OrganizationUnitId))
                .Select(a => a.SalesPersonId));
        }

        return [.. result.Distinct()];
    }

    public IReadOnlyList<Guid> GetDescendantOrgUnitIds(IReadOnlyList<SalesOrganizationUnitDto> allUnits, Guid unitId)
    {
        var childrenByParent = allUnits
            .Where(u => u.ParentOrganizationUnitId.HasValue)
            .GroupBy(u => u.ParentOrganizationUnitId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(u => u.Id).ToList());

        var result = new List<Guid>();
        var visited = new HashSet<Guid> { unitId };
        var queue = new Queue<Guid>();
        queue.Enqueue(unitId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children)) continue;
            foreach (var childId in children)
            {
                if (!visited.Add(childId)) continue; // cycle guard
                result.Add(childId);
                queue.Enqueue(childId);
            }
        }

        return result;
    }

    public IReadOnlyList<Guid> GetAncestorOrgUnitIds(IReadOnlyList<SalesOrganizationUnitDto> allUnits, Guid unitId)
    {
        var byId = allUnits.ToDictionary(u => u.Id);
        var result = new List<Guid>();
        var visited = new HashSet<Guid> { unitId };

        var current = byId.GetValueOrDefault(unitId)?.ParentOrganizationUnitId;
        while (current.HasValue)
        {
            if (!visited.Add(current.Value)) break; // cycle guard
            result.Add(current.Value);
            current = byId.GetValueOrDefault(current.Value)?.ParentOrganizationUnitId;
        }

        return result;
    }

    private static Dictionary<Guid, int> BuildRoleLevelLookup(IReadOnlyList<SalesRoleDto> roles)
        => roles.ToDictionary(r => r.Id, r => r.HierarchyLevel ?? UnrankedLevel);

    private static Dictionary<Guid, List<SalesAssignmentDto>> BuildActivePrimaryByUnit(
        IReadOnlyList<SalesAssignmentDto> assignments)
        => assignments
            .Where(a => a.ActiveStatus && a.PrimaryAssignmentFlag)
            .GroupBy(a => a.OrganizationUnitId)
            .ToDictionary(g => g.Key, g => g.ToList());
}
