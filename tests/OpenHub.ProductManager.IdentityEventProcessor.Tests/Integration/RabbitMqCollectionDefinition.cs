using Xunit;

namespace OpenHub.ProductManager.IdentityEventProcessor.Tests.Integration;

[CollectionDefinition(Name)]
public sealed class RabbitMqCollectionDefinition : ICollectionFixture<RabbitMqCollectionFixture>
{
    public const string Name = "RabbitMq Integration";
}
