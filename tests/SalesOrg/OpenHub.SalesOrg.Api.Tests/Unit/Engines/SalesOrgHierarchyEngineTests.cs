using Shouldly;
using Xunit;
using OpenHub.SalesOrg.Api.Managers.Engines;
using OpenHub.SalesOrg.Api.Managers.Models;

namespace OpenHub.SalesOrg.Api.Tests.Unit.Engines;

public class SalesOrgHierarchyEngineTests
{
    private static SalesRoleDto MakeRole(Guid id, string name, int? level) => new()
    {
        Id = id,
        Name = name,
        HierarchyLevel = level,
        ActiveStatus = true,
    };

    private static SalesOrganizationUnitDto MakeUnit(Guid id, Guid? parentId, string name) => new()
    {
        Id = id,
        Name = name,
        OrganizationUnitType = "territory",
        ParentOrganizationUnitId = parentId,
        ActiveStatus = true,
    };

    private static SalesAssignmentDto MakeAssignment(
        Guid personId, Guid unitId, Guid roleId, bool active = true, bool primary = true) => new()
    {
        Id = Guid.NewGuid(),
        SalesPersonId = personId,
        OrganizationUnitId = unitId,
        RoleId = roleId,
        EffectiveStartDate = new DateOnly(2024, 1, 1),
        ActiveStatus = active,
        PrimaryAssignmentFlag = primary,
    };

    private readonly SalesOrgHierarchyEngine _engine = new();

    [Fact]
    public void GetManagerChain_SingleLevelUnit_RepOutrankedByManagerInSameUnit()
    {
        var unitId = Guid.NewGuid();
        var repRoleId = Guid.NewGuid();
        var managerRoleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var roles = new List<SalesRoleDto> { MakeRole(repRoleId, "Rep", 1), MakeRole(managerRoleId, "Manager", 2) };
        var units = new List<SalesOrganizationUnitDto> { MakeUnit(unitId, null, "Territory") };
        var assignments = new List<SalesAssignmentDto>
        {
            MakeAssignment(repId, unitId, repRoleId),
            MakeAssignment(managerId, unitId, managerRoleId),
        };

        var chain = _engine.GetManagerChain(assignments, roles, units, repId);

        chain.ShouldBe([managerId]);
    }

    [Fact]
    public void GetManagerChain_MultiLevelInUnitOutrankChain_ReturnsEachOutrankerInOrder()
    {
        var unitId = Guid.NewGuid();
        var level1RoleId = Guid.NewGuid();
        var level2RoleId = Guid.NewGuid();
        var level3RoleId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var midId = Guid.NewGuid();
        var topId = Guid.NewGuid();

        var roles = new List<SalesRoleDto>
        {
            MakeRole(level1RoleId, "Rep", 1),
            MakeRole(level2RoleId, "Team Lead", 2),
            MakeRole(level3RoleId, "Unit Head", 3),
        };
        var units = new List<SalesOrganizationUnitDto> { MakeUnit(unitId, null, "Territory") };
        var assignments = new List<SalesAssignmentDto>
        {
            MakeAssignment(personId, unitId, level1RoleId),
            MakeAssignment(midId, unitId, level2RoleId),
            MakeAssignment(topId, unitId, level3RoleId),
        };

        var chain = _engine.GetManagerChain(assignments, roles, units, personId);

        chain.ShouldBe([midId, topId]);
    }

    [Fact]
    public void GetManagerChain_MultiLevelParentClimb_ReturnsTopOfEachAncestorUnit()
    {
        var territoryUnitId = Guid.NewGuid();
        var districtUnitId = Guid.NewGuid();
        var regionUnitId = Guid.NewGuid();
        var repRoleId = Guid.NewGuid();
        var districtManagerRoleId = Guid.NewGuid();
        var regionManagerRoleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var districtManagerId = Guid.NewGuid();
        var regionManagerId = Guid.NewGuid();

        var roles = new List<SalesRoleDto>
        {
            MakeRole(repRoleId, "Rep", 1),
            MakeRole(districtManagerRoleId, "District Manager", 2),
            MakeRole(regionManagerRoleId, "Region Manager", 3),
        };
        var units = new List<SalesOrganizationUnitDto>
        {
            MakeUnit(territoryUnitId, districtUnitId, "Territory"),
            MakeUnit(districtUnitId, regionUnitId, "District"),
            MakeUnit(regionUnitId, null, "Region"),
        };
        var assignments = new List<SalesAssignmentDto>
        {
            MakeAssignment(repId, territoryUnitId, repRoleId),
            MakeAssignment(districtManagerId, districtUnitId, districtManagerRoleId),
            MakeAssignment(regionManagerId, regionUnitId, regionManagerRoleId),
        };

        var chain = _engine.GetManagerChain(assignments, roles, units, repId);

        chain.ShouldBe([districtManagerId, regionManagerId]);
    }

    [Fact]
    public void GetManagerChain_RootUnitWithNoParent_TerminatesWithEmptyChainForTopPerson()
    {
        var unitId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var personId = Guid.NewGuid();

        var roles = new List<SalesRoleDto> { MakeRole(roleId, "Global Manager", 5) };
        var units = new List<SalesOrganizationUnitDto> { MakeUnit(unitId, null, "Global") };
        var assignments = new List<SalesAssignmentDto> { MakeAssignment(personId, unitId, roleId) };

        var chain = _engine.GetManagerChain(assignments, roles, units, personId);

        chain.ShouldBeEmpty();
    }

    [Fact]
    public void GetReports_CrossSubtree_TopOfUnitGetsPeersPlusEntireDescendantSubtree()
    {
        var parentUnitId = Guid.NewGuid();
        var childUnitId = Guid.NewGuid();
        var grandchildUnitId = Guid.NewGuid();
        var managerRoleId = Guid.NewGuid();
        var repRoleId = Guid.NewGuid();

        var managerId = Guid.NewGuid();
        var peerRepId = Guid.NewGuid();
        var childRepId = Guid.NewGuid();
        var grandchildRepId = Guid.NewGuid();

        var roles = new List<SalesRoleDto>
        {
            MakeRole(managerRoleId, "Manager", 3),
            MakeRole(repRoleId, "Rep", 1),
        };
        var units = new List<SalesOrganizationUnitDto>
        {
            MakeUnit(parentUnitId, null, "Region"),
            MakeUnit(childUnitId, parentUnitId, "District"),
            MakeUnit(grandchildUnitId, childUnitId, "Territory"),
        };
        var assignments = new List<SalesAssignmentDto>
        {
            MakeAssignment(managerId, parentUnitId, managerRoleId),
            MakeAssignment(peerRepId, parentUnitId, repRoleId),
            MakeAssignment(childRepId, childUnitId, repRoleId),
            MakeAssignment(grandchildRepId, grandchildUnitId, repRoleId),
        };

        var reports = _engine.GetReports(assignments, roles, units, managerId);

        reports.ShouldBe([peerRepId, childRepId, grandchildRepId], ignoreOrder: true);
    }

    [Fact]
    public void GetReports_NotTopOfUnit_OnlyReturnsLowerRankedSameUnitPeers()
    {
        var unitId = Guid.NewGuid();
        var childUnitId = Guid.NewGuid();
        var topRoleId = Guid.NewGuid();
        var midRoleId = Guid.NewGuid();
        var lowRoleId = Guid.NewGuid();

        var topId = Guid.NewGuid();
        var midId = Guid.NewGuid();
        var lowId = Guid.NewGuid();
        var descendantId = Guid.NewGuid();

        var roles = new List<SalesRoleDto>
        {
            MakeRole(topRoleId, "Top", 3),
            MakeRole(midRoleId, "Mid", 2),
            MakeRole(lowRoleId, "Low", 1),
        };
        var units = new List<SalesOrganizationUnitDto>
        {
            MakeUnit(unitId, null, "Region"),
            MakeUnit(childUnitId, unitId, "District"),
        };
        var assignments = new List<SalesAssignmentDto>
        {
            MakeAssignment(topId, unitId, topRoleId),
            MakeAssignment(midId, unitId, midRoleId),
            MakeAssignment(lowId, unitId, lowRoleId),
            MakeAssignment(descendantId, childUnitId, lowRoleId),
        };

        var reports = _engine.GetReports(assignments, roles, units, midId);

        // mid is not the top of the unit (top outranks them), so only lower-ranked same-unit
        // peers come back — the descendant subtree is NOT included.
        reports.ShouldBe([lowId]);
    }

    [Fact]
    public void GetDescendantOrgUnitIds_CyclicGraph_TerminatesAndDoesNotDuplicate()
    {
        // A deliberately cyclic org-unit graph (a bad reparent edit): unit A's parent is B, and
        // B's parent is A.
        var unitAId = Guid.NewGuid();
        var unitBId = Guid.NewGuid();
        var unitCId = Guid.NewGuid();

        var units = new List<SalesOrganizationUnitDto>
        {
            MakeUnit(unitAId, unitBId, "A"),
            MakeUnit(unitBId, unitAId, "B"),
            MakeUnit(unitCId, unitAId, "C"),
        };

        var descendants = _engine.GetDescendantOrgUnitIds(units, unitAId);

        // Must terminate (this call returning at all proves the cycle guard works) and must not
        // contain unitAId itself or duplicate entries.
        descendants.ShouldBe([unitBId, unitCId], ignoreOrder: true);
        descendants.Count.ShouldBe(descendants.Distinct().Count());
    }

    [Fact]
    public void GetAncestorOrgUnitIds_CyclicGraph_TerminatesAndDoesNotDuplicate()
    {
        var unitAId = Guid.NewGuid();
        var unitBId = Guid.NewGuid();

        var units = new List<SalesOrganizationUnitDto>
        {
            MakeUnit(unitAId, unitBId, "A"),
            MakeUnit(unitBId, unitAId, "B"),
        };

        var ancestors = _engine.GetAncestorOrgUnitIds(units, unitAId);

        ancestors.ShouldBe([unitBId]);
    }

    [Fact]
    public void GetDescendantOrgUnitIds_LeafUnit_ReturnsEmpty()
    {
        var unitId = Guid.NewGuid();
        var units = new List<SalesOrganizationUnitDto> { MakeUnit(unitId, null, "Leaf") };

        var descendants = _engine.GetDescendantOrgUnitIds(units, unitId);

        descendants.ShouldBeEmpty();
    }

    [Fact]
    public void GetAncestorOrgUnitIds_RootUnit_ReturnsEmpty()
    {
        var unitId = Guid.NewGuid();
        var units = new List<SalesOrganizationUnitDto> { MakeUnit(unitId, null, "Root") };

        var ancestors = _engine.GetAncestorOrgUnitIds(units, unitId);

        ancestors.ShouldBeEmpty();
    }
}
