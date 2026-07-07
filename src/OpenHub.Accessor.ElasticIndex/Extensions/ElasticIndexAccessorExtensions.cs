using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenHub.Accessor.ElasticIndex.Interfaces;
using OpenHub.Accessor.ElasticIndex.Models;

namespace OpenHub.Accessor.ElasticIndex.Extensions;

public static class ElasticIndexAccessorExtensions
{
    /// <summary>
    /// Registers tenant-isolated Elasticsearch indexing for TDocument: the
    /// generic accessor, a shared Vault secret accessor (safe to call once even
    /// if a consuming service registers multiple document types), and a hosted
    /// service that bootstraps TDocument's index and writer key at startup.
    ///
    /// Prerequisite: the consuming service must separately register the
    /// bootstrap ElasticsearchClient (e.g. via Aspire's
    /// builder.AddElasticsearchClient("Elasticsearch")) — this library only
    /// consumes that client, it doesn't create it, since the connection name
    /// and Aspire wiring are the consuming service's own concern.
    /// </summary>
    public static IServiceCollection AddElasticIndexAccessor<TDocument>(this IServiceCollection services)
        where TDocument : class, ITenantScopedDocument
    {
        services.TryAddSingleton<IVaultSecretAccessor, VaultSecretAccessor>();
        services.AddSingleton<IElasticIndexAccessor<TDocument>, ElasticIndexAccessor<TDocument>>();
        services.AddHostedService<ElasticIndexInitializer<TDocument>>();

        return services;
    }
}
