using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Managers.Interfaces;

public interface ICatalogManager
{
    Task<LookupItemDto> CreateManufacturerAsync(CreateManufacturerRequest request, CancellationToken ct = default);
    Task<LookupItemDto> CreateBrandAsync(CreateBrandRequest request, CancellationToken ct = default);
}
