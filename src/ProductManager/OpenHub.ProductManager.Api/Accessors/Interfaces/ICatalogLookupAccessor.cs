using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors.Interfaces;

public interface ICatalogLookupAccessor
{
    ValueTask<IReadOnlyList<ManufacturerEntity>> GetManufacturersAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<BrandEntity>> GetBrandsAsync(CancellationToken ct = default);
    Task<ManufacturerEntity> InsertManufacturerAsync(string name, Guid tenantId, CancellationToken ct = default);
    Task<BrandEntity> InsertBrandAsync(string name, Guid tenantId, CancellationToken ct = default);
}
