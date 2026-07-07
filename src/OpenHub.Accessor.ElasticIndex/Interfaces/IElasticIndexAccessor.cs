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

    /// <summary>Upserts a document, keyed by its own ID, through its tenant's filtered alias.</summary>
    Task IndexDocumentAsync(TDocument document, CancellationToken ct = default);

    /// <summary>Deletes a document by ID, through its tenant's filtered alias. A missing document is not an error.</summary>
    Task DeleteDocumentAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new, timestamped backing index (e.g. "products-20260707020000") with the
    /// same tenant_id mapping as the shared index, for a full reindex to write into before
    /// any tenant alias is repointed to it. Does not affect any existing alias.
    /// </summary>
    Task<string> CreateVersionedIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Bulk-upserts documents directly into the named index (bypasses the tenant alias — used
    /// during a full reindex before cutover). Returns the IDs of documents that failed to index
    /// (e.g. malformed data) — a partial failure does not throw, since callers with a
    /// tolerance threshold (e.g. "skip up to 5% of a tenant's batch") need to keep going. A
    /// request-level failure (the whole bulk call itself failing, not a per-item rejection)
    /// still throws.
    /// </summary>
    Task<IReadOnlyList<Guid>> BulkIndexDocumentsAsync(string indexName, IReadOnlyList<TDocument> documents, CancellationToken ct = default);

    /// <summary>
    /// Atomically re-points a tenant's filtered alias from whatever index it currently targets
    /// to newIndexName, preserving the same tenant_id term filter. Single POST _aliases call —
    /// the alias never resolves to zero or two indices mid-swap. Returns the index name(s) the
    /// alias previously pointed at (normally exactly one) — a full-reindex caller collects these
    /// across every tenant to know what's safe to try deleting once every tenant has cut over.
    /// </summary>
    Task<IReadOnlyList<string>> RepointTenantAliasAsync(Guid tenantId, string newIndexName, CancellationToken ct = default);

    /// <summary>Deletes indexName only if no alias in the cluster currently references it (checked live via GET _alias, not trusted from caller state).</summary>
    Task DeleteIndexIfUnreferencedAsync(string indexName, CancellationToken ct = default);
}
