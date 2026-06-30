using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Managers.Engines.Interfaces;

public interface IProductPricingEngine
{
    IReadOnlyList<ProductPriceDto> HydratePrices(
        IEnumerable<ProductPriceDto> prices,
        IReadOnlyDictionary<Guid, string> priceTypeNames,
        IReadOnlyDictionary<Guid, string> currencyCodes);

    ProductPriceDto? GetCurrentPrice(
        IEnumerable<ProductPriceDto> prices,
        string priceTypeCode,
        IReadOnlyDictionary<Guid, string> priceTypeNames);
}
