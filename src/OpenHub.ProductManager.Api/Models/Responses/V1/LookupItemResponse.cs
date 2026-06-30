using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Responses.V1;

public class LookupItemResponse
{
    [Description("Unique identifier. Use this value when referencing the item from a request.")]
    public Guid Id { get; set; }

    [Description("Short machine-readable code (e.g. NEW, IN_STOCK). Null for tenant-managed items without one.")]
    public string? Code { get; set; }

    [Description("Display name shown to end users.")]
    public string Name { get; set; } = null!;
}
