using Xunit;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Integration;

[CollectionDefinition(Name)]
public sealed class ElasticsearchCollectionDefinition : ICollectionFixture<ElasticsearchCollectionFixture>
{
    public const string Name = "Elasticsearch Integration";
}
