using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Responses.V1;

public class ProductPriceResponse
{
    [Description("Unique price record identifier.")]
    public Guid Id { get; set; }

    [Description("Resolved price type name (e.g. Retail, Wholesale, Cost).")]
    public string? PriceTypeName { get; set; }

    [Description("ISO 4217 currency code (e.g. USD, EUR, GBP).")]
    public string? CurrencyCode { get; set; }

    [Description("Price amount in the specified currency.")]
    public decimal Amount { get; set; }

    [Description("Date from which this price is effective (UTC).")]
    public DateTimeOffset EffectiveFrom { get; set; }

    [Description("Date until which this price is effective (UTC). Null means the price has no expiry.")]
    public DateTimeOffset? EffectiveUntil { get; set; }
}
