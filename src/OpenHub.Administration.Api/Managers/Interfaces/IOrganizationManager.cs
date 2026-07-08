using OpenHub.Administration.Api.Managers.Models;

namespace OpenHub.Administration.Api.Managers.Interfaces;

public interface IOrganizationManager
{
    Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationDto request, CancellationToken ct = default);

    Task<OrganizationDto?> GetOrganizationAsync(string realmSlug, CancellationToken ct = default);

    Task<IReadOnlyList<OrganizationDto>> ListOrganizationsAsync(CancellationToken ct = default);
}
