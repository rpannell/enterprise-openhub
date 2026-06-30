namespace OpenHub.ProductManager.Api.Managers.Models;

public class ProductPriceDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid PriceTypeId { get; set; }
    public string? PriceTypeName { get; set; }
    public Guid CurrencyId { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
    public bool IsActive { get; set; }
}
