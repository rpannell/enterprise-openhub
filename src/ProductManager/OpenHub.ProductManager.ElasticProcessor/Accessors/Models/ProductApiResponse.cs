namespace OpenHub.ProductManager.ElasticProcessor.Accessors.Models;

/// <summary>
/// Minimal subset of ProductManager.Api's ProductResponse — just what's needed
/// until the Elastic-indexing shape is designed. Field names match the API's
/// PascalCase System.Text.Json output exactly (no custom naming policy either side).
/// </summary>
public class ProductApiResponse
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsPublished { get; set; }
}
