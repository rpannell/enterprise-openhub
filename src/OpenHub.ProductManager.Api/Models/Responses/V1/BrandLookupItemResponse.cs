using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Responses.V1;

public class BrandLookupItemResponse
{
    [Description("Unique identifier. Use this value when referencing the brand from a request.")]
    public Guid Id { get; set; }

    [Description("Short code assigned by the tenant. Null if not set.")]
    public string? Code { get; set; }

    [Description("Display name shown to end users.")]
    public string Name { get; set; } = null!;

    [Description("Owning manufacturer, if this brand was linked to one. Null if unlinked.")]
    public Guid? ManufacturerId { get; set; }
}
