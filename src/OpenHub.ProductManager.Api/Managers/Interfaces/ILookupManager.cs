using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Managers.Interfaces;

public interface ILookupManager
{
    Task<LookupsDto> GetAllAsync(CancellationToken ct = default);
}
