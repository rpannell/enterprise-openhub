using OpenHub.ProductManager.Api.Engines.Interfaces;
using OpenHub.ProductManager.Api.Models.Dtos;

namespace OpenHub.ProductManager.Api.Engines;

public class ProductPricingEngine : IProductPricingEngine
{
    public IReadOnlyList<ProductPriceDto> HydratePrices(
        IEnumerable<ProductPriceDto> prices,
        IReadOnlyDictionary<Guid, string> priceTypeNames,
        IReadOnlyDictionary<Guid, string> currencyCodes)
        => prices.Select(p =>
        {
            p.PriceTypeName = priceTypeNames.GetValueOrDefault(p.PriceTypeId);
            p.CurrencyCode = currencyCodes.GetValueOrDefault(p.CurrencyId);
            return p;
        }).ToList();

    public ProductPriceDto? GetCurrentPrice(
        IEnumerable<ProductPriceDto> prices,
        string priceTypeCode,
        IReadOnlyDictionary<Guid, string> priceTypeNames)
    {
        var now = DateTimeOffset.UtcNow;
        return prices
            .Where(p => p.IsActive
                && priceTypeNames.GetValueOrDefault(p.PriceTypeId) == priceTypeCode
                && p.EffectiveFrom <= now
                && (p.EffectiveUntil == null || p.EffectiveUntil > now))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefault();
    }
}
