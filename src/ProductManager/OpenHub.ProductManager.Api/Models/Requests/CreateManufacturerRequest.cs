using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Requests;

public class CreateManufacturerRequest
{
    [Description("Manufacturer display name. Must be unique within the tenant. Max 200 characters.")]
    public string Name { get; set; } = null!;
}
