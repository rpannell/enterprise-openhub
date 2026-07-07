# OpenHub.Accessor.ElasticIndex

Shared, tenant-isolated Elasticsearch indexing for any domain in OpenHub — `ElasticIndexAccessor<TDocument>` owns one shared index per document type, with per-tenant filtered aliases and API-key-scoped access. First consumer: `OpenHub.ProductManager.ElasticProcessor`'s `ProductDocument`. Any other domain (e.g. a future helpdesk ticket index) opts in by implementing `ITenantScopedDocument` and calling `AddElasticIndexAccessor<TDocument>()` — no other wiring needed.

## Tenant isolation model

Mirrors how this codebase enforces tenant isolation in Postgres (`FORCE ROW LEVEL SECURITY`) — at the data-access boundary, not by trusting the caller:

- **One shared index per document type** (`TDocument.IndexName`), not one index per tenant.
- **Per-tenant filtered alias** — `{IndexName}-tenant-{tenantId}`, defined with a `term` filter on the document's `tenant_id` field. Anything querying through that alias only ever sees that tenant's documents.
- **Per-tenant reader API key** — restricted, by Elasticsearch role descriptor, to `read` on only that tenant's alias. A consumer retrieves this key from Vault to query Elasticsearch directly — it is physically incapable of reading another tenant's alias or the raw underlying index, enforced by Elasticsearch's security layer itself, not by convention.

Both the alias and reader key are created lazily, on `EnsureTenantProvisionedAsync`'s first call for a given tenant, gated by a `HybridCache`-backed idempotency check so this isn't a network+Vault round-trip on every call.

## Authentication: API-key-only past startup

The Elasticsearch **superuser** password is used exactly once, internally, to bootstrap a scoped **writer key** (minted on first use, cached for the process lifetime). From then on, all index/alias management and document writes go through a second client authenticated with that writer key — the superuser credential is never used again after bootstrap.

Both the writer key and each tenant's reader key are persisted to Vault via `IVaultSecretAccessor`, since Elasticsearch only ever returns an API key's raw secret once, at creation:
- Writer key: `elastic-index/writer-key/{IndexName}`
- Tenant reader key: `elastic-index/tenant-keys/{IndexName}/{tenantId}`

Two Elasticsearch-specific security rules shaped this design, worth knowing before changing it:
1. **Alias-management privilege must cover the alias name, not just the source index.** `PutAlias`'s underlying action (`indices:admin/aliases`) checks the caller's granted index-name patterns against *both* the source index and the alias being created. The writer key's role descriptor grants `{IndexName}` and `{IndexName}-tenant-*` — not a blanket `{IndexName}*` wildcard, which could also cover an unrelated document type's differently-named index sharing the same prefix.
2. **A "derived" API key (one authenticated as another API key) cannot mint an API key with real privileges** — Elasticsearch requires derived-key creation to use an empty role descriptor, as an anti-privilege-escalation measure. Both the writer key's own creation and every tenant reader key are therefore minted via the **superuser bootstrap client**, never via the writer key itself.

## Explicit mapping gotcha (read before touching `EnsureIndexAsync`)

Only the `tenant_id` field gets an explicit (keyword) mapping; everything else in `TDocument` is left to Elasticsearch's dynamic mapping. That field is mapped by **literal string** (`p.Keyword("tenant_id")`), not by an expression on the property (`p.Keyword(f => f.TenantId)`) — when `TDocument` is a generic type parameter (as it is here), `Elastic.Clients.Elasticsearch`'s expression-based mapping API resolves the field name from the C# property name's default camelCase policy, **ignoring `[JsonPropertyName]`**. That silently creates an unused `tenantId` keyword field while the real `tenant_id` field (the one actually serialized, and the one the alias filter targets) falls through to dynamic mapping as analyzed text — breaking the term filter with no error anywhere. This only reproduces with a genuine generic type parameter; a concrete, closed type in the same expression resolves the attribute correctly, which is what makes this bug easy to reintroduce by accident during a refactor.

Every `TDocument` implementing `ITenantScopedDocument` **must** serialize its `TenantId` property as `tenant_id` (via `[JsonPropertyName("tenant_id")]`) — this is a hard requirement of the shared accessor, not just a naming convention.

## Usage

```csharp
// TDocument
public class ProductDocument : ITenantScopedDocument
{
    public static string IndexName => "products";

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; set; }

    // ...other fields, dynamically mapped
}

// Program.cs — register the bootstrap client (superuser-authenticated) yourself;
// this library only consumes it, it doesn't create it.
builder.AddElasticsearchClient("Elasticsearch");

// Accessors/Setup/AccessorServiceExtensions.cs (or equivalent)
services.AddElasticIndexAccessor<ProductDocument>();
```

```csharp
// Wherever you orchestrate writes (a Manager, in iDesign terms)
await indexAccessor.EnsureTenantProvisionedAsync(tenantId, ct);
await indexAccessor.IndexDocumentAsync(document, ct);
// ...
await indexAccessor.DeleteDocumentAsync(id, ct);
```

`AddElasticIndexAccessor<TDocument>()` registers, per closed generic type:
- `IElasticIndexAccessor<TDocument>` (singleton)
- `IVaultSecretAccessor` (singleton, shared across every `TDocument` registered in the same service — safe to call this method more than once per process)
- `ElasticIndexInitializer<TDocument>` (`IHostedService` — bootstraps the index and writer key eagerly at startup, so a bootstrap failure surfaces immediately rather than on the first message)

## Testing

Correctness here (alias filtering, API-key scoping) is only meaningfully proven against a real Elasticsearch cluster — see `tests/OpenHub.ProductManager.ElasticProcessor.Tests/Integration/ProductIndexAccessorIntegrationTests.cs` for the `Testcontainers.Elasticsearch`-based integration suite (index/alias isolation, upsert semantics, delete, and a cross-tenant API-key rejection test). If this library grows its own dedicated test project, move those tests here instead of leaving them attached to a single consumer.
