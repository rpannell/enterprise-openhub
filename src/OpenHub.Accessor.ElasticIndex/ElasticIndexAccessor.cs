using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenHub.Accessor.ElasticIndex.Interfaces;
using OpenHub.Accessor.ElasticIndex.Models;

namespace OpenHub.Accessor.ElasticIndex;

/// <summary>
/// Owns all Elasticsearch I/O for one shared index family (TDocument.IndexName): index
/// bootstrap, per-tenant filtered aliases, and API-key issuance. Tenant isolation
/// is enforced the same way OpenHub enforces it in Postgres (FORCE ROW LEVEL
/// SECURITY) — at the data-access boundary, not by trusting the caller: a
/// per-tenant alias filtered to that tenant's documents, and a per-tenant API
/// key whose role descriptor restricts it to only that alias.
///
/// Per-document writes (IndexDocumentAsync/DeleteDocumentAsync) always go through a
/// tenant's alias, never a hardcoded index name — this is what lets a full reindex
/// (CreateVersionedIndexAsync + BulkIndexDocumentsAsync + RepointTenantAliasAsync) move
/// one tenant's alias to a new backing index without any change needed here or in
/// callers: the next per-document write for that tenant transparently lands in
/// whichever index its alias currently targets.
///
/// The client injected here (via the consuming service's
/// builder.AddElasticsearchClient call) authenticates as the Elasticsearch
/// superuser — it is used exactly once, to bootstrap a scoped writer API key,
/// and never again. From that point on, every operation (index/alias
/// management, document writes, per-tenant key creation) goes through a second
/// client authenticated with that writer key. Both the writer key and each
/// tenant's reader key are persisted to Vault, since Elasticsearch only ever
/// returns a key's raw secret once, at creation.
///
/// Generic over any TDocument implementing ITenantScopedDocument — one closed
/// generic instance per model that needs this pattern (see
/// ElasticIndexAccessorExtensions.AddElasticIndexAccessor for how a consuming
/// service opts a model in). Only TDocument's tenant_id field gets an explicit
/// (keyword) mapping, since that's the one field this accessor's own logic
/// depends on (the alias filter requires an exact-match field, not ES's default
/// dynamically-mapped analyzed text); every other field is left to
/// Elasticsearch's dynamic mapping. This trades per-model mapping precision for
/// genericity — a model with specific mapping needs (e.g. a field that must not
/// be analyzed, or needs a non-default type) should map it via its own accessor
/// instead of this shared one.
/// </summary>
public class ElasticIndexAccessor<TDocument>(
    ElasticsearchClient bootstrapClient,
    IVaultSecretAccessor vault,
    HybridCache cache,
    IConfiguration configuration,
    ILogger<ElasticIndexAccessor<TDocument>> logger) : IElasticIndexAccessor<TDocument>
    where TDocument : class, ITenantScopedDocument
{
    private static readonly string IndexName = TDocument.IndexName;
    private static readonly string WriterKeyVaultPath = $"elastic-index/writer-key/{IndexName}";
    private static readonly string TenantKeyVaultPathPrefix = $"elastic-index/tenant-keys/{IndexName}";
    private const string ApiKeyVaultField = "api_key";

    private static readonly HybridCacheEntryOptions TenantProvisionedCacheOptions = new()
    {
        // Long-lived: aliases/keys don't change once created, and both PutAlias and
        // CreateApiKey below are themselves idempotent (Vault-backed), so this only
        // needs to avoid a network+Vault round-trip on every message, not act as the
        // source of truth for correctness.
        Expiration = TimeSpan.FromHours(6),
        LocalCacheExpiration = TimeSpan.FromHours(6),
    };

    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private ElasticsearchClient? _writerClient;

    private static string AliasName(Guid tenantId) => $"{IndexName}-tenant-{tenantId}";
    private static string TenantKeyVaultPath(Guid tenantId) => $"{TenantKeyVaultPathPrefix}/{tenantId}";
    private static string TenantProvisionedCacheKey(Guid tenantId) => $"es-tenant-ensured:{IndexName}:{tenantId}";

    public async Task InitializeAsync(CancellationToken ct = default)
        => await GetWriterClientAsync(ct);

    public async Task EnsureTenantProvisionedAsync(Guid tenantId, CancellationToken ct = default)
    {
        await cache.GetOrCreateAsync(
            TenantProvisionedCacheKey(tenantId),
            async innerCt =>
            {
                var client = await GetWriterClientAsync(innerCt);
                var alias = AliasName(tenantId);

                var aliasResponse = await client.Indices.PutAliasAsync(IndexName, alias, a => a
                    .Filter(f => f.Term(t => t.Field("tenant_id").Value(tenantId.ToString()))),
                    innerCt);

                if (!aliasResponse.IsValidResponse)
                    throw new InvalidOperationException(
                        $"Failed to ensure alias '{alias}': {aliasResponse.DebugInformation}");

                var existingKey = await vault.ReadAsync(TenantKeyVaultPath(tenantId), ApiKeyVaultField, innerCt);
                if (existingKey is null)
                {
                    // Elasticsearch refuses to let an API key mint another API key
                    // with actual privileges ("derived" keys must have an empty role
                    // descriptor) — a deliberate anti-privilege-escalation rule. Minting
                    // has to go through the superuser bootstrap client; only the alias
                    // write above uses the writer's own (day-to-day) API key.
                    var keyResponse = await bootstrapClient.Security.CreateApiKeyAsync(k => k
                        .Name(alias)
                        .RoleDescriptors(r => r.Add($"{IndexName}-tenant-reader", d => d
                            .Indices(idx => idx.Names(alias).Privileges("read")))),
                        innerCt);

                    if (!keyResponse.IsValidResponse || keyResponse.Encoded is null)
                        throw new InvalidOperationException(
                            $"Failed to create reader API key for tenant {tenantId}: {keyResponse.DebugInformation}");

                    await vault.WriteAsync(TenantKeyVaultPath(tenantId), ApiKeyVaultField, keyResponse.Encoded, innerCt);
                }

                logger.LogInformation("Ensured filtered alias {Alias} and reader API key for tenant {TenantId}.", alias, tenantId);
                return true;
            },
            TenantProvisionedCacheOptions,
            cancellationToken: ct);
    }

    public async Task IndexDocumentAsync(TDocument document, CancellationToken ct = default)
    {
        var client = await GetWriterClientAsync(ct);
        var alias = AliasName(document.TenantId);
        var response = await client.IndexAsync(document, i => i.Index(alias).Id(document.Id.ToString()), ct);

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Failed to index document {document.Id}: {response.DebugInformation}");
    }

    public async Task DeleteDocumentAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var client = await GetWriterClientAsync(ct);
        var response = await client.DeleteAsync(AliasName(tenantId), id.ToString(), ct);

        // NotFound is a valid outcome (nothing to delete) — only a real failure should throw.
        if (!response.IsValidResponse && response.Result != Result.NotFound)
            throw new InvalidOperationException($"Failed to delete document {id}: {response.DebugInformation}");
    }

    public async Task<string> CreateVersionedIndexAsync(CancellationToken ct = default)
    {
        var client = await GetWriterClientAsync(ct);
        // fffffff (ticks) rather than just seconds — a full reindex run only calls this once,
        // but tests and any manual re-trigger in quick succession would otherwise collide on
        // the same name within the same second.
        var versionedIndexName = $"{IndexName}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}";

        // Same tenant_id-only explicit mapping as EnsureIndexAsync — see the type-level
        // remarks on why literal-string field mapping is required for a generic TDocument.
        var response = await client.Indices.CreateAsync(versionedIndexName, c => c
            .Mappings(m => m.Properties(p => p.Keyword("tenant_id"))),
            ct);

        if (!response.IsValidResponse)
            throw new InvalidOperationException(
                $"Failed to create versioned index '{versionedIndexName}': {response.DebugInformation}");

        logger.LogInformation("Created versioned Elasticsearch index {IndexName}.", versionedIndexName);
        return versionedIndexName;
    }

    public async Task<IReadOnlyList<Guid>> BulkIndexDocumentsAsync(string indexName, IReadOnlyList<TDocument> documents, CancellationToken ct = default)
    {
        if (documents.Count == 0)
            return [];

        var client = await GetWriterClientAsync(ct);
        var response = await client.BulkAsync(b => b
            .Index(indexName)
            .IndexMany(documents, (descriptor, document) => descriptor.Id(document.Id.ToString())),
            ct);

        if (!response.IsValidResponse && !response.Errors)
            throw new InvalidOperationException($"Bulk index request against '{indexName}' failed: {response.DebugInformation}");

        if (!response.Errors)
            return [];

        var failedIds = new List<Guid>();
        foreach (var item in response.Items)
        {
            if (item.Error is null)
                continue;

            if (Guid.TryParse(item.Id, out var failedId))
                failedIds.Add(failedId);

            logger.LogWarning(
                "Bulk index of document {DocumentId} into {IndexName} failed: {Error}",
                item.Id, indexName, item.Error.Reason);
        }

        return failedIds;
    }

    public async Task<IReadOnlyList<string>> RepointTenantAliasAsync(Guid tenantId, string newIndexName, CancellationToken ct = default)
    {
        var client = await GetWriterClientAsync(ct);
        var alias = AliasName(tenantId);

        var existingAliasIndices = await client.Indices.GetAliasAsync(a => a.Name(alias), ct);
        var previousIndices = existingAliasIndices is { IsValidResponse: true, Values: not null }
            ? existingAliasIndices.Values.Keys.Where(name => name != newIndexName).ToList()
            : [];

        // Each array element becomes its own "actions" entry in the _aliases request body —
        // a single Action<IndexUpdateAliasesActionDescriptor> lambda can only ever populate
        // ONE add/remove per JSON object, so remove(s) and the add must be separate elements,
        // not separate method calls chained on the same descriptor (which silently drops all
        // but the last call and leaves the alias pointing at both old and new indices).
        var actions = previousIndices
            .Select(previousIndex => (Action<IndexUpdateAliasesActionDescriptor>)(a => a.Remove(r => r.Index(previousIndex).Alias(alias))))
            .Append(a => a.Add(add => add.Index(newIndexName).Alias(alias)
                .Filter(f => f.Term(t => t.Field("tenant_id").Value(tenantId.ToString())))))
            .ToArray();

        var response = await client.Indices.UpdateAliasesAsync(u => u.Actions(actions), ct);

        if (!response.IsValidResponse)
            throw new InvalidOperationException(
                $"Failed to repoint alias '{alias}' to '{newIndexName}': {response.DebugInformation}");

        logger.LogInformation("Repointed alias {Alias} to {NewIndexName} (previously: {PreviousIndices}).",
            alias, newIndexName, previousIndices.Count == 0 ? "none" : string.Join(", ", previousIndices));

        return previousIndices;
    }

    public async Task DeleteIndexIfUnreferencedAsync(string indexName, CancellationToken ct = default)
    {
        var client = await GetWriterClientAsync(ct);

        var aliasCheck = await client.Indices.GetAliasAsync(a => a.Indices(indexName), ct);
        if (aliasCheck is { IsValidResponse: true, Values: not null }
            && aliasCheck.Values.TryGetValue(indexName, out var indexAliases) && indexAliases.Aliases.Count > 0)
        {
            logger.LogWarning(
                "Not deleting index {IndexName} — still referenced by alias(es): {Aliases}.",
                indexName, string.Join(", ", indexAliases.Aliases.Keys));
            return;
        }

        var response = await client.Indices.DeleteAsync(indexName, ct);
        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Failed to delete index '{indexName}': {response.DebugInformation}");

        logger.LogInformation("Deleted superseded Elasticsearch index {IndexName}.", indexName);
    }

    private async Task<ElasticsearchClient> GetWriterClientAsync(CancellationToken ct)
    {
        if (_writerClient is not null)
            return _writerClient;

        await _clientLock.WaitAsync(ct);
        try
        {
            if (_writerClient is not null)
                return _writerClient;

            await EnsureIndexAsync(ct);

            var apiKey = await vault.ReadAsync(WriterKeyVaultPath, ApiKeyVaultField, ct);
            if (apiKey is null)
            {
                var keyResponse = await bootstrapClient.Security.CreateApiKeyAsync(k => k
                    .Name($"{IndexName}-writer")
                    .RoleDescriptors(r => r.Add($"{IndexName}-writer", d => d
                        // No cluster privileges needed — API-key minting (both this
                        // key's own creation and every tenant reader key) always goes
                        // through the superuser bootstrap client, since Elasticsearch
                        // forbids a "derived" API key from creating another key with
                        // any privileges. This key only ever does index/alias/document
                        // I/O, so index-scoped privileges are all it needs.
                        // Elasticsearch's alias-management check (indices:admin/aliases,
                        // used by PutAlias/UpdateAliases) authorizes against BOTH the
                        // source index and the alias name being created — an exact-match
                        // pattern on just IndexName is not enough to also create
                        // "{IndexName}-tenant-{id}" aliases or "{IndexName}-{timestamp}"
                        // versioned indices created by a full reindex. Grant the index
                        // itself plus a single "{IndexName}-*" pattern that covers both —
                        // not a blanket "{IndexName}*" (no dash), which could also cover
                        // an unrelated model's differently-named index sharing the same
                        // prefix without a separator.
                        .Indices(idx => idx.Names(IndexName, $"{IndexName}-*").Privileges("all")))),
                    ct);

                if (!keyResponse.IsValidResponse || keyResponse.Encoded is null)
                    throw new InvalidOperationException(
                        $"Failed to create Elasticsearch writer API key for index '{IndexName}': {keyResponse.DebugInformation}");

                apiKey = keyResponse.Encoded;
                await vault.WriteAsync(WriterKeyVaultPath, ApiKeyVaultField, apiKey, ct);
                logger.LogInformation("Created Elasticsearch writer API key for index {IndexName} and stored it in Vault.", IndexName);
            }

            var connectionString = configuration.GetConnectionString("Elasticsearch")
                ?? throw new InvalidOperationException("ConnectionStrings:Elasticsearch is not configured.");

            var settings = new ElasticsearchClientSettings(new Uri(new Uri(connectionString).GetLeftPart(UriPartial.Authority)))
                .Authentication(new Elastic.Transport.ApiKey(apiKey))
                // Elasticsearch's dev containers (Aspire's and the raw Docker image
                // alike) serve a self-signed cert when TLS is on — there's no CA to
                // pin in local dev, so this mirrors the same permissive-trust
                // convention consuming services already use for dev (e.g. plain HTTP
                // or ASPIRE_ALLOW_UNSECURED_TRANSPORT). Harmless if the endpoint is
                // plain HTTP instead.
                .ServerCertificateValidationCallback((_, _, _, _) => true);

            _writerClient = new ElasticsearchClient(settings);
            return _writerClient;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private async Task EnsureIndexAsync(CancellationToken ct)
    {
        var exists = await bootstrapClient.Indices.ExistsAsync(IndexName, ct);
        if (exists.Exists)
            return;

        // Only the tenant_id field gets an explicit mapping — see the type-level
        // remarks on why the rest of TDocument is left to Elasticsearch's dynamic
        // mapping. Mapped by literal field name, NOT via an expression on
        // TDocument's property (f => f.TenantId): when TDocument is a generic type
        // parameter (as it is here), Elastic.Clients.Elasticsearch's expression-based
        // mapping resolves the field name from the C# property name's default
        // camelCase policy, NOT from its [JsonPropertyName] attribute — so it would
        // map a field called "tenantId", while the document is actually serialized
        // (correctly, attribute-respecting) as "tenant_id". Two different fields:
        // the mapped one sits unused, and the real one falls through to dynamic
        // mapping as analyzed text, silently breaking the alias term filter below.
        // The raw string here matches the same literal field name already hardcoded
        // in the alias filter's .Field("tenant_id") call.
        var response = await bootstrapClient.Indices.CreateAsync(IndexName, c => c
            .Mappings(m => m.Properties(p => p.Keyword("tenant_id"))),
            ct);

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Failed to create Elasticsearch index '{IndexName}': {response.DebugInformation}");

        logger.LogInformation("Created Elasticsearch index {IndexName}.", IndexName);
    }
}
