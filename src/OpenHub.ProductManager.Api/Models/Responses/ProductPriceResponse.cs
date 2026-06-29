namespace OpenHub.ProductManager.Api.Models.Responses;

public class ProductPriceResponse
{
    public Guid Id { get; set; }
    public string? PriceTypeName { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
}
