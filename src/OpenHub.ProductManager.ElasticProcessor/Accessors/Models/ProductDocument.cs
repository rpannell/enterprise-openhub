using System.Text.Json.Serialization;
using OpenHub.Accessor.ElasticIndex.Models;

namespace OpenHub.ProductManager.ElasticProcessor.Accessors.Models;

/// <summary>
/// Elasticsearch document shape for the shared "products" index. Distinct from
/// ProductApiResponse (the API's HTTP DTO) because it carries TenantId, which the
/// API DTO has no need for. Explicit snake_case property names mirror this
/// codebase's Postgres column-naming convention and — for TenantId specifically —
/// must match the "tenant_id" field name the per-tenant alias filter is defined
/// against exactly, since the client's default camelCase naming would otherwise
/// silently break that filter.
/// </summary>
public class ProductDocument : ITenantScopedDocument
{
    public static string IndexName => "products";

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; set; }

    [JsonPropertyName("sku")]
    public string Sku { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_published")]
    public bool IsPublished { get; set; }
}
