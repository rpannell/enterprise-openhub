using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenHub.Accessor.ElasticIndex.Interfaces;
using OpenHub.Accessor.ElasticIndex.Models;

namespace OpenHub.Accessor.ElasticIndex;

/// <summary>
/// Ensures TDocument's shared Elasticsearch index and its writer API key exist —
/// run eagerly on every startup so a bootstrap failure surfaces immediately
/// rather than on the first message. Registered per closed generic document type
/// by AddElasticIndexAccessor&lt;TDocument&gt; — a consuming service that registers
/// multiple document types gets one of these per type, each independent.
/// </summary>
public class ElasticIndexInitializer<TDocument>(
    IElasticIndexAccessor<TDocument> accessor,
    ILogger<ElasticIndexInitializer<TDocument>> logger) : IHostedService
    where TDocument : class, ITenantScopedDocument
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await accessor.InitializeAsync(cancellationToken);
        logger.LogInformation("Elasticsearch index bootstrap complete for {IndexName}.", TDocument.IndexName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
