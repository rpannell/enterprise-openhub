using Xunit;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Integration;

[CollectionDefinition(Name)]
public sealed class RabbitMqCollectionDefinition : ICollectionFixture<RabbitMqCollectionFixture>
{
    public const string Name = "RabbitMq Integration";
}
