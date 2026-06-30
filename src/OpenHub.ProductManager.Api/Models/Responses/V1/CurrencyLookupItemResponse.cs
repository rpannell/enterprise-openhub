using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Responses.V1;

public class CurrencyLookupItemResponse
{
    [Description("Unique identifier. Use this value when referencing the currency from a request.")]
    public Guid Id { get; set; }

    [Description("ISO 4217 currency code (e.g. USD, EUR, GBP).")]
    public string Code { get; set; } = null!;

    [Description("Display name (e.g. US Dollar).")]
    public string Name { get; set; } = null!;

    [Description("Display symbol (e.g. $).")]
    public string Symbol { get; set; } = null!;
}
