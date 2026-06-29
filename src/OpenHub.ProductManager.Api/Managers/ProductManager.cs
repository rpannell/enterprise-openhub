using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Engines.Interfaces;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Dtos;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Managers;

public class ProductManager(
    IProductAccessor productAccessor,
    IProductPriceAccessor priceAccessor,
    ILookupCacheAccessor lookupCache,
    IProductEngine productEngine,
    IProductPricingEngine pricingEngine,
    ProductMapper mapper) : IProductManager
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
    {
        // 1. Fetch from DB — single-table, no joins
        var entities = await productAccessor.GetAllAsync(ct);
        var dtos = mapper.ToDtoList(entities);

        // 2. Fetch lookup dictionaries from HybridCache
        var manufacturers = await lookupCache.GetManufacturerNamesAsync(ct);
        var brands = await lookupCache.GetBrandNamesAsync(ct);
        var conditions = await lookupCache.GetConditionTypeNamesAsync(ct);
        var statuses = await lookupCache.GetAvailabilityStatusNamesAsync(ct);
        var uoms = await lookupCache.GetUomCodesAsync(ct);

        // 3. Engine hydrates — pure in-memory join, no I/O
        return productEngine.HydrateAll(dtos, manufacturers, brands, conditions, statuses, uoms);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await productAccessor.GetByIdAsync(id, ct);
        if (entity is null) return null;

        var dto = mapper.ToDto(entity);

        var manufacturers = await lookupCache.GetManufacturerNamesAsync(ct);
        var brands = await lookupCache.GetBrandNamesAsync(ct);
        var conditions = await lookupCache.GetConditionTypeNamesAsync(ct);
        var statuses = await lookupCache.GetAvailabilityStatusNamesAsync(ct);
        var uoms = await lookupCache.GetUomCodesAsync(ct);

        productEngine.Hydrate(dto, manufacturers, brands, conditions, statuses, uoms);

        // Fetch and hydrate prices
        var priceEntities = await priceAccessor.GetActiveByProductIdAsync(id, ct);
        var priceDtos = mapper.ToDtoPriceList(priceEntities);
        var priceTypes = await lookupCache.GetPriceTypeNamesAsync(ct);
        var currencies = await lookupCache.GetCurrencyCodesAsync(ct);

        dto.Prices = pricingEngine.HydratePrices(priceDtos, priceTypes, currencies).ToList();

        return dto;
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, Guid createUserId, CancellationToken ct = default)
    {
        var dto = mapper.ToDto(request);
        dto.CreateUserId = createUserId;
        dto.ModifyUserId = createUserId;

        var entity = mapper.ToEntity(dto);
        var created = await productAccessor.CreateAsync(entity, ct);
        return mapper.ToDto(created);
    }

    public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductRequest request, Guid modifyUserId, CancellationToken ct = default)
    {
        var existing = await productAccessor.GetByIdAsync(id, ct);
        if (existing is null) return null;

        // Apply non-null fields from request
        if (request.Name is not null) existing.Name = request.Name;
        if (request.Description is not null) existing.Description = request.Description;
        if (request.ShortDescription is not null) existing.ShortDescription = request.ShortDescription;
        if (request.ManufacturerId.HasValue) existing.ManufacturerId = request.ManufacturerId;
        if (request.BrandId.HasValue) existing.BrandId = request.BrandId;
        if (request.ConditionTypeId.HasValue) existing.ConditionTypeId = request.ConditionTypeId;
        if (request.AvailabilityStatusId.HasValue) existing.AvailabilityStatusId = request.AvailabilityStatusId;
        if (request.ManufacturerPartNumber is not null) existing.ManufacturerPartNumber = request.ManufacturerPartNumber;
        if (request.IsActive.HasValue) existing.IsActive = request.IsActive.Value;
        if (request.IsPublished.HasValue) existing.IsPublished = request.IsPublished.Value;
        if (request.Weight.HasValue) existing.Weight = request.Weight;
        if (request.WeightUomId.HasValue) existing.WeightUomId = request.WeightUomId;
        existing.ModifyUserId = modifyUserId;

        var updated = await productAccessor.UpdateAsync(existing, ct);
        return mapper.ToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default)
    {
        var existing = await productAccessor.GetByIdAsync(id, ct);
        if (existing is null) return false;

        await productAccessor.SoftDeleteAsync(id, deleteUserId, ct);
        return true;
    }
}
