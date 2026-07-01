using Microsoft.AspNetCore.Http;
using OpenHub.Accessor.RabbitMq;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Managers.Engines.Interfaces;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Contracts;

namespace OpenHub.ProductManager.Api.Managers;

public class ProductManager(
    IProductAccessor productAccessor,
    IProductPriceAccessor priceAccessor,
    ILookupCacheAccessor lookupCache,
    ICatalogLookupAccessor catalogLookup,
    IProductEngine productEngine,
    IProductPricingEngine pricingEngine,
    ProductMapper mapper,
    IHttpContextAccessor http,
    IMessagePublisher messagePublisher) : IProductManager
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    private string TenantSlug =>
        http.HttpContext?.Items["TenantSlug"] as string
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await productAccessor.GetAllAsync(ct);
        var dtos = mapper.ToDtoList(entities);

        var manufacturers = await GetManufacturerNamesAsync(ct);
        var brands = await GetBrandNamesAsync(ct);
        var conditions = await lookupCache.GetConditionTypeNamesAsync(ct);
        var statuses = await lookupCache.GetAvailabilityStatusNamesAsync(ct);
        var uoms = await lookupCache.GetUomCodesAsync(ct);

        return productEngine.HydrateAll(dtos, manufacturers, brands, conditions, statuses, uoms);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await productAccessor.GetByIdAsync(id, ct);
        if (entity is null) return null;

        var dto = mapper.ToDto(entity);

        var manufacturers = await GetManufacturerNamesAsync(ct);
        var brands = await GetBrandNamesAsync(ct);
        var conditions = await lookupCache.GetConditionTypeNamesAsync(ct);
        var statuses = await lookupCache.GetAvailabilityStatusNamesAsync(ct);
        var uoms = await lookupCache.GetUomCodesAsync(ct);

        productEngine.Hydrate(dto, manufacturers, brands, conditions, statuses, uoms);

        var priceEntities = await priceAccessor.GetActiveByProductIdAsync(id, ct);
        var priceDtos = mapper.ToDtoPriceList(priceEntities);
        var priceTypes = await lookupCache.GetPriceTypeNamesAsync(ct);
        var currencies = await lookupCache.GetCurrencyCodesAsync(ct);

        dto.Prices = pricingEngine.HydratePrices(priceDtos, priceTypes, currencies).ToList();

        return dto;
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, Guid createUserId, CancellationToken ct = default)
    {
        var manufacturerId = await ResolveManufacturerAsync(request.Manufacturer, ct);
        var brandId = await ResolveBrandAsync(request.Brand, ct);

        var dto = mapper.ToDto(request);
        dto.TenantId = TenantId;
        dto.ManufacturerId = manufacturerId;
        dto.BrandId = brandId;
        dto.CreateUserId = createUserId;
        dto.ModifyUserId = createUserId;

        var entity = mapper.ToEntity(dto);
        var created = await productAccessor.CreateAsync(entity, ct);

        await messagePublisher.PublishAsync(new ProductChangedEvent
        {
            ProductId = created.Id,
            TenantId = TenantId,
            UserId = createUserId,
            RealmSlug = TenantSlug,
        }, cancellationToken: ct);

        return mapper.ToDto(created);
    }

    public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductRequest request, Guid modifyUserId, CancellationToken ct = default)
    {
        var existing = await productAccessor.GetByIdAsync(id, ct);
        if (existing is null) return null;

        if (request.Name is not null) existing.Name = request.Name;
        if (request.Description is not null) existing.Description = request.Description;
        if (request.ShortDescription is not null) existing.ShortDescription = request.ShortDescription;
        if (request.Manufacturer is not null)
            existing.ManufacturerId = string.IsNullOrEmpty(request.Manufacturer)
                ? null
                : await ResolveManufacturerAsync(request.Manufacturer, ct);
        if (request.Brand is not null)
            existing.BrandId = string.IsNullOrEmpty(request.Brand)
                ? null
                : await ResolveBrandAsync(request.Brand, ct);
        if (request.ConditionTypeId.HasValue) existing.ConditionTypeId = request.ConditionTypeId;
        if (request.AvailabilityStatusId.HasValue) existing.AvailabilityStatusId = request.AvailabilityStatusId;
        if (request.ManufacturerPartNumber is not null) existing.ManufacturerPartNumber = request.ManufacturerPartNumber;
        if (request.IsActive.HasValue) existing.IsActive = request.IsActive.Value;
        if (request.IsPublished.HasValue) existing.IsPublished = request.IsPublished.Value;
        if (request.Weight.HasValue) existing.Weight = request.Weight;
        if (request.WeightUomId.HasValue) existing.WeightUomId = request.WeightUomId;
        existing.ModifyUserId = modifyUserId;

        var updated = await productAccessor.UpdateAsync(existing, ct);

        await messagePublisher.PublishAsync(new ProductChangedEvent
        {
            ProductId = updated.Id,
            TenantId = TenantId,
            UserId = modifyUserId,
            RealmSlug = TenantSlug,
        }, cancellationToken: ct);

        return mapper.ToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default)
    {
        var existing = await productAccessor.GetByIdAsync(id, ct);
        if (existing is null) return false;

        await productAccessor.SoftDeleteAsync(id, deleteUserId, ct);

        await messagePublisher.PublishAsync(new ProductChangedEvent
        {
            ProductId = id,
            TenantId = TenantId,
            UserId = deleteUserId,
            RealmSlug = TenantSlug,
            IsDeleted = true,
        }, cancellationToken: ct);

        return true;
    }

    private async Task<Guid> ResolveManufacturerAsync(string name, CancellationToken ct)
    {
        var all = await catalogLookup.GetManufacturersAsync(ct);
        var match = all.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new KeyNotFoundException($"Manufacturer '{name}' not found in your catalog.");
        return match.Id;
    }

    private async Task<Guid> ResolveBrandAsync(string name, CancellationToken ct)
    {
        var all = await catalogLookup.GetBrandsAsync(ct);
        var match = all.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new KeyNotFoundException($"Brand '{name}' not found in your catalog.");
        return match.Id;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetManufacturerNamesAsync(CancellationToken ct)
        => (await catalogLookup.GetManufacturersAsync(ct)).ToDictionary(m => m.Id, m => m.Name);

    private async Task<IReadOnlyDictionary<Guid, string>> GetBrandNamesAsync(CancellationToken ct)
        => (await catalogLookup.GetBrandsAsync(ct)).ToDictionary(b => b.Id, b => b.Name);
}
