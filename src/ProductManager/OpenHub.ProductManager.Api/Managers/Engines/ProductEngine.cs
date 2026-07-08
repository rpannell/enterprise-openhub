using OpenHub.ProductManager.Api.Managers.Engines.Interfaces;
using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Managers.Engines;

public class ProductEngine : IProductEngine
{
    public ProductDto Hydrate(
        ProductDto dto,
        IReadOnlyDictionary<Guid, string> manufacturerNames,
        IReadOnlyDictionary<Guid, string> brandNames,
        IReadOnlyDictionary<Guid, string> conditionTypeNames,
        IReadOnlyDictionary<Guid, string> availabilityStatusNames,
        IReadOnlyDictionary<Guid, string> uomCodes)
    {
        if (dto.ManufacturerId.HasValue)
            dto.ManufacturerName = manufacturerNames.GetValueOrDefault(dto.ManufacturerId.Value);

        if (dto.BrandId.HasValue)
            dto.BrandName = brandNames.GetValueOrDefault(dto.BrandId.Value);

        if (dto.ConditionTypeId.HasValue)
            dto.ConditionTypeName = conditionTypeNames.GetValueOrDefault(dto.ConditionTypeId.Value);

        if (dto.AvailabilityStatusId.HasValue)
            dto.AvailabilityStatusName = availabilityStatusNames.GetValueOrDefault(dto.AvailabilityStatusId.Value);

        if (dto.WeightUomId.HasValue)
            dto.WeightUomCode = uomCodes.GetValueOrDefault(dto.WeightUomId.Value);

        return dto;
    }

    public IReadOnlyList<ProductDto> HydrateAll(
        IEnumerable<ProductDto> dtos,
        IReadOnlyDictionary<Guid, string> manufacturerNames,
        IReadOnlyDictionary<Guid, string> brandNames,
        IReadOnlyDictionary<Guid, string> conditionTypeNames,
        IReadOnlyDictionary<Guid, string> availabilityStatusNames,
        IReadOnlyDictionary<Guid, string> uomCodes)
        => dtos.Select(dto => Hydrate(dto, manufacturerNames, brandNames,
                conditionTypeNames, availabilityStatusNames, uomCodes))
              .ToList();
}
