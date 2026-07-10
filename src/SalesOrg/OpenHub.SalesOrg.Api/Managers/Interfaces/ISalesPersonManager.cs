using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Managers.Interfaces;

public interface ISalesPersonManager
{
    Task<IReadOnlyList<SalesPersonDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Keycloak users in the caller's realm who aren't already linked to a sales person —
    /// backs the "new sales person" picker, which only ever links an existing Keycloak identity
    /// rather than typing name/email freehand.
    /// </summary>
    Task<IReadOnlyList<AvailableKeycloakUserDto>> GetAvailableKeycloakUsersAsync(CancellationToken ct = default);
    Task<SalesPersonDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SalesPersonDto> CreateAsync(CreateSalesPersonRequest request, Guid createUserId, CancellationToken ct = default);
    Task<SalesPersonDto?> UpdateAsync(Guid id, UpdateSalesPersonRequest request, Guid modifyUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The person's manager chain, nearest link first: in-unit outrankers, then each successive
    /// parent unit's top-ranked assignee.
    /// </summary>
    Task<IReadOnlyList<SalesPersonDto>> GetManagerChainAsync(Guid salesPersonId, CancellationToken ct = default);

    /// <summary>
    /// Same-unit peers this person outranks, plus — if this person is their unit's top — every
    /// person assigned anywhere in the unit's descendant subtree.
    /// </summary>
    Task<IReadOnlyList<SalesPersonDto>> GetReportsAsync(Guid salesPersonId, CancellationToken ct = default);
}
