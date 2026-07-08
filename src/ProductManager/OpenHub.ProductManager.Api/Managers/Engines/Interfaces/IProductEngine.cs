using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Managers.Engines.Interfaces;

public interface IProductEngine
{
    ProductDto Hydrate(
        ProductDto dto,
        IReadOnlyDictionary<Guid, string> manufacturerNames,
        IReadOnlyDictionary<Guid, string> brandNames,
        IReadOnlyDictionary<Guid, string> conditionTypeNames,
        IReadOnlyDictionary<Guid, string> availabilityStatusNames,
        IReadOnlyDictionary<Guid, string> uomCodes);

    IReadOnlyList<ProductDto> HydrateAll(
        IEnumerable<ProductDto> dtos,
        IReadOnlyDictionary<Guid, string> manufacturerNames,
        IReadOnlyDictionary<Guid, string> brandNames,
        IReadOnlyDictionary<Guid, string> conditionTypeNames,
        IReadOnlyDictionary<Guid, string> availabilityStatusNames,
        IReadOnlyDictionary<Guid, string> uomCodes);
}
