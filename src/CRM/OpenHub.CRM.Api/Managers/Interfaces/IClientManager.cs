using OpenHub.CRM.Api.Managers.Models;
using OpenHub.CRM.Api.Models.Requests;

namespace OpenHub.CRM.Api.Managers.Interfaces;

public interface IClientManager
{
    Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken ct = default);
    Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClientDto> CreateAsync(CreateClientRequest request, Guid createUserId, CancellationToken ct = default);
    Task<ClientDto?> UpdateAsync(Guid id, UpdateClientRequest request, Guid modifyUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default);
}
