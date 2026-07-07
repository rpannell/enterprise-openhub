using OpenHub.Accessor.ElasticIndex.Models;

namespace OpenHub.Accessor.ElasticIndex.Interfaces;

/// <summary>
/// Generic tenant-isolated Elasticsearch index accessor. One closed generic
/// instance (e.g. IElasticIndexAccessor&lt;ProductDocument&gt;) owns exactly one
/// shared index (TDocument.IndexName) — register a new closed generic per model
/// that needs this pattern (see ElasticIndexAccessorExtensions), no other wiring needed.
/// </summary>
public interface IElasticIndexAccessor<TDocument> where TDocument : class, ITenantScopedDocument
{
    /// <summary>Ensures the shared index exists and a scoped writer API key has been minted. Called once at startup.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Idempotently ensures the tenant's filtered alias and reader API key exist.</summary>
    Task EnsureTenantProvisionedAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Upserts a document, keyed by its own ID, into the shared index.</summary>
    Task IndexDocumentAsync(TDocument document, CancellationToken ct = default);

    /// <summary>Deletes a document by ID. A missing document is not an error.</summary>
    Task DeleteDocumentAsync(Guid id, CancellationToken ct = default);
}
