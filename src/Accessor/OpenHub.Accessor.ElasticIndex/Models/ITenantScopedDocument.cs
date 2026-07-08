namespace OpenHub.Accessor.ElasticIndex.Models;

/// <summary>
/// Contract for any document type indexed via the generic ElasticIndexAccessor&lt;TDocument&gt;.
/// IndexName is a static interface member (not per-instance) so one index/alias/
/// API-key scheme binds to a document type at compile time — DI registers
/// IElasticIndexAccessor&lt;ProductDocument&gt;, IElasticIndexAccessor&lt;OrderDocument&gt;,
/// etc. as plain closed-generic registrations, with no per-model factory wiring.
/// </summary>
public interface ITenantScopedDocument
{
    /// <summary>The shared Elasticsearch index all documents of this type are written to, e.g. "products".</summary>
    static abstract string IndexName { get; }

    Guid Id { get; }

    Guid TenantId { get; }
}
