using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using OpenHub.Accessor.ElasticIndex;
using OpenHub.Accessor.ElasticIndex.Interfaces;
using OpenHub.ProductManager.Contracts;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Integration;

/// <summary>
/// Drives ElasticIndexAccessor&lt;ProductDocument&gt; against a real Elasticsearch
/// container — the thing that actually matters here (tenant isolation via a
/// filtered alias, and via an API-key role descriptor restricted to that alias)
/// is only meaningfully proven against a real cluster, not a mocked ElasticsearchClient.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ElasticsearchCollectionDefinition.Name)]
public class ProductIndexAccessorIntegrationTests(ElasticsearchCollectionFixture fixture)
{
    private sealed class InMemoryVaultSecretAccessor : IVaultSecretAccessor
    {
        private readonly Dictionary<string, string> _values = [];

        public Task<string?> ReadAsync(string path, string key, CancellationToken ct = default)
            => Task.FromResult(_values.GetValueOrDefault($"{path}/{key}"));

        public Task WriteAsync(string path, string key, string value, CancellationToken ct = default)
        {
            _values[$"{path}/{key}"] = value;
            return Task.CompletedTask;
        }
    }

    private static Uri BaseUri(string connectionString)
    {
        var uri = new Uri(connectionString);
        return new Uri($"{uri.Scheme}://{uri.Host}:{uri.Port}");
    }

    private static ElasticsearchClient CreateBootstrapClient(string connectionString)
    {
        var settings = new ElasticsearchClientSettings(BaseUri(connectionString))
            .Authentication(new BasicAuthentication("elastic", ElasticsearchCollectionFixture.Password))
            .ServerCertificateValidationCallback((_, _, _, _) => true);
        return new ElasticsearchClient(settings);
    }

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private ElasticIndexAccessor<ProductDocument> CreateAccessor(out InMemoryVaultSecretAccessor vault)
    {
        vault = new InMemoryVaultSecretAccessor();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Elasticsearch"] = BaseUri(fixture.ConnectionString).ToString(),
            })
            .Build();

        return new ElasticIndexAccessor<ProductDocument>(
            CreateBootstrapClient(fixture.ConnectionString),
            vault,
            CreateCache(),
            configuration,
            NullLogger<ElasticIndexAccessor<ProductDocument>>.Instance);
    }

    private static ElasticsearchClient CreateReaderClient(string connectionString, string encodedApiKey)
    {
        var settings = new ElasticsearchClientSettings(BaseUri(connectionString))
            .Authentication(new ApiKey(encodedApiKey))
            .ServerCertificateValidationCallback((_, _, _, _) => true);
        return new ElasticsearchClient(settings);
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        var accessor = CreateAccessor(out _);

        await accessor.InitializeAsync();
        await Should.NotThrowAsync(() => accessor.InitializeAsync());
    }

    [Fact]
    public async Task EnsureTenantProvisionedAsync_IsolatesDocumentsByTenantAlias()
    {
        var accessor = CreateAccessor(out _);
        await accessor.InitializeAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var productA = new ProductDocument { Id = Guid.NewGuid(), TenantId = tenantA, Sku = "A-1", Name = "Product A" };
        var productB = new ProductDocument { Id = Guid.NewGuid(), TenantId = tenantB, Sku = "B-1", Name = "Product B" };

        await accessor.EnsureTenantProvisionedAsync(tenantA);
        await accessor.EnsureTenantProvisionedAsync(tenantB);
        await accessor.IndexDocumentAsync(productA);
        await accessor.IndexDocumentAsync(productB);

        var bootstrapClient = CreateBootstrapClient(fixture.ConnectionString);
        var refreshResponse = await bootstrapClient.Indices.RefreshAsync(ProductDocument.IndexName);
        refreshResponse.IsValidResponse.ShouldBeTrue(refreshResponse.DebugInformation);

        var aliasAResponse = await bootstrapClient.SearchAsync<ProductDocument>(s => s.Indices($"products-tenant-{tenantA}"));
        var aliasBResponse = await bootstrapClient.SearchAsync<ProductDocument>(s => s.Indices($"products-tenant-{tenantB}"));

        aliasAResponse.Documents.ShouldContain(d => d.Id == productA.Id);
        aliasAResponse.Documents.ShouldNotContain(d => d.Id == productB.Id);

        aliasBResponse.Documents.ShouldContain(d => d.Id == productB.Id);
        aliasBResponse.Documents.ShouldNotContain(d => d.Id == productA.Id);
    }

    [Fact]
    public async Task IndexDocumentAsync_SameProductId_UpsertsRatherThanDuplicates()
    {
        var accessor = CreateAccessor(out _);
        await accessor.InitializeAsync();

        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await accessor.EnsureTenantProvisionedAsync(tenantId);

        await accessor.IndexDocumentAsync(new ProductDocument { Id = productId, TenantId = tenantId, Sku = "S-1", Name = "Original" });
        await accessor.IndexDocumentAsync(new ProductDocument { Id = productId, TenantId = tenantId, Sku = "S-1", Name = "Updated" });

        var bootstrapClient = CreateBootstrapClient(fixture.ConnectionString);
        var refreshResponse = await bootstrapClient.Indices.RefreshAsync(ProductDocument.IndexName);
        refreshResponse.IsValidResponse.ShouldBeTrue(refreshResponse.DebugInformation);

        var response = await bootstrapClient.SearchAsync<ProductDocument>(s => s.Indices($"products-tenant-{tenantId}"));

        response.Documents.Count(d => d.Id == productId).ShouldBe(1);
        response.Documents.Single(d => d.Id == productId).Name.ShouldBe("Updated");
    }

    [Fact]
    public async Task DeleteDocumentAsync_RemovesDocument()
    {
        var accessor = CreateAccessor(out _);
        await accessor.InitializeAsync();

        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await accessor.EnsureTenantProvisionedAsync(tenantId);
        await accessor.IndexDocumentAsync(new ProductDocument { Id = productId, TenantId = tenantId, Sku = "S-2", Name = "ToDelete" });

        await accessor.DeleteDocumentAsync(tenantId, productId);

        var bootstrapClient = CreateBootstrapClient(fixture.ConnectionString);
        var refreshResponse = await bootstrapClient.Indices.RefreshAsync(ProductDocument.IndexName);
        refreshResponse.IsValidResponse.ShouldBeTrue(refreshResponse.DebugInformation);

        var response = await bootstrapClient.SearchAsync<ProductDocument>(s => s.Indices($"products-tenant-{tenantId}"));
        response.Documents.ShouldNotContain(d => d.Id == productId);
    }

    [Fact]
    public async Task TenantReaderApiKey_CannotQueryAnotherTenantsAlias()
    {
        var accessor = CreateAccessor(out var vault);
        await accessor.InitializeAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await accessor.EnsureTenantProvisionedAsync(tenantA);
        await accessor.EnsureTenantProvisionedAsync(tenantB);

        var tenantAKey = await vault.ReadAsync($"elastic-index/tenant-keys/{ProductDocument.IndexName}/{tenantA}", "api_key");
        tenantAKey.ShouldNotBeNull();

        var tenantAReaderClient = CreateReaderClient(fixture.ConnectionString, tenantAKey!);

        var ownAliasResponse = await tenantAReaderClient.SearchAsync<ProductDocument>(s => s.Indices($"products-tenant-{tenantA}"));
        ownAliasResponse.IsValidResponse.ShouldBeTrue();

        var otherAliasResponse = await tenantAReaderClient.SearchAsync<ProductDocument>(s => s.Indices($"products-tenant-{tenantB}"));
        otherAliasResponse.IsValidResponse.ShouldBeFalse();

        var rawIndexResponse = await tenantAReaderClient.SearchAsync<ProductDocument>(s => s.Indices(ProductDocument.IndexName));
        rawIndexResponse.IsValidResponse.ShouldBeFalse();
    }

    [Fact]
    public async Task FullReindexCutover_MovesTenantAliasAndTrickleWritesLandInNewIndex()
    {
        var accessor = CreateAccessor(out _);
        await accessor.InitializeAsync();

        var tenantId = Guid.NewGuid();
        var oldProductId = Guid.NewGuid();
        await accessor.EnsureTenantProvisionedAsync(tenantId);
        await accessor.IndexDocumentAsync(new ProductDocument { Id = oldProductId, TenantId = tenantId, Sku = "OLD-1", Name = "Pre-reindex" });

        var newIndexName = await accessor.CreateVersionedIndexAsync();
        var reindexedProductId = Guid.NewGuid();
        var failedIds = await accessor.BulkIndexDocumentsAsync(newIndexName,
            [new ProductDocument { Id = reindexedProductId, TenantId = tenantId, Sku = "NEW-1", Name = "Reindexed" }]);
        failedIds.ShouldBeEmpty();

        await accessor.RepointTenantAliasAsync(tenantId, newIndexName);

        var bootstrapClient = CreateBootstrapClient(fixture.ConnectionString);
        var refreshResponse = await bootstrapClient.Indices.RefreshAsync(newIndexName);
        refreshResponse.IsValidResponse.ShouldBeTrue(refreshResponse.DebugInformation);

        // Alias now resolves only to the new index's document, not the pre-cutover one.
        var aliasResponse = await bootstrapClient.SearchAsync<ProductDocument>(s => s.Indices($"products-tenant-{tenantId}"));
        aliasResponse.Documents.ShouldContain(d => d.Id == reindexedProductId);
        aliasResponse.Documents.ShouldNotContain(d => d.Id == oldProductId);

        // A trickle-style write after cutover goes through the alias, landing in the new index automatically.
        var postCutoverProductId = Guid.NewGuid();
        await accessor.IndexDocumentAsync(new ProductDocument { Id = postCutoverProductId, TenantId = tenantId, Sku = "NEW-2", Name = "Post-cutover" });
        await bootstrapClient.Indices.RefreshAsync(newIndexName);

        var postCutoverResponse = await bootstrapClient.SearchAsync<ProductDocument>(s => s.Indices(newIndexName));
        postCutoverResponse.Documents.ShouldContain(d => d.Id == postCutoverProductId);
    }

    [Fact]
    public async Task DeleteIndexIfUnreferencedAsync_RefusesToDeleteAnIndexStillAliased()
    {
        var accessor = CreateAccessor(out _);
        await accessor.InitializeAsync();

        var tenantId = Guid.NewGuid();
        await accessor.EnsureTenantProvisionedAsync(tenantId);

        await Should.NotThrowAsync(() => accessor.DeleteIndexIfUnreferencedAsync(ProductDocument.IndexName));

        var bootstrapClient = CreateBootstrapClient(fixture.ConnectionString);
        var existsResponse = await bootstrapClient.Indices.ExistsAsync(ProductDocument.IndexName);
        existsResponse.Exists.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteIndexIfUnreferencedAsync_DeletesOnceNoAliasPointsAtIt()
    {
        var accessor = CreateAccessor(out _);
        await accessor.InitializeAsync();

        var tenantId = Guid.NewGuid();
        await accessor.EnsureTenantProvisionedAsync(tenantId);

        // Deliberately does not target the shared legacy ProductDocument.IndexName here — other
        // tests in this same integration run leave their own tenants aliased to it, so it would
        // always be "still referenced" for reasons unrelated to this test. Instead, cut this one
        // tenant over twice, between two versioned indices it exclusively owns, to prove deletion
        // is gated on the live alias state rather than a caller-trusted "I'm done with it" flag.
        var firstVersionedIndex = await accessor.CreateVersionedIndexAsync();
        await accessor.RepointTenantAliasAsync(tenantId, firstVersionedIndex);

        var secondVersionedIndex = await accessor.CreateVersionedIndexAsync();
        await accessor.RepointTenantAliasAsync(tenantId, secondVersionedIndex);

        await accessor.DeleteIndexIfUnreferencedAsync(firstVersionedIndex);

        var bootstrapClient = CreateBootstrapClient(fixture.ConnectionString);
        var existsResponse = await bootstrapClient.Indices.ExistsAsync(firstVersionedIndex);
        existsResponse.Exists.ShouldBeFalse();
    }
}
