using Testcontainers.Elasticsearch;
using Xunit;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Integration;

/// <summary>
/// Starts a single real Elasticsearch container once for the whole integration
/// test class (see <see cref="ElasticsearchCollectionDefinition"/>), mirroring
/// RabbitMqCollectionFixture's shape. Security is enabled with a known bootstrap
/// password so ProductIndexAccessor's writer-key bootstrap path is exercised for
/// real, not bypassed.
/// </summary>
public sealed class ElasticsearchCollectionFixture : IAsyncLifetime
{
    public const string Password = "Dev_P@ssw0rd1!";

    private ElasticsearchContainer? _container;

    public string ConnectionString => _container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        _container = new ElasticsearchBuilder("elasticsearch:8.17.3")
            .WithPassword(Password)
            // Constrain heap explicitly — the container's default heap sizing
            // otherwise risks an OOM kill (exit 137) in memory-constrained dev
            // Docker Desktop VMs, especially alongside other running containers.
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            // Leave TLS on its stock default (enabled, self-signed cert) —
            // Testcontainers.Elasticsearch's own readiness probe expects HTTPS;
            // disabling it here makes the probe hang forever even though the
            // container is actually healthy. Both test-side clients and
            // ElasticIndexAccessor's internally-built writer client instead bypass
            // certificate validation (see CreateBootstrapClient/CreateReaderClient
            // and ElasticIndexAccessor's writer-client construction).
            .Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
