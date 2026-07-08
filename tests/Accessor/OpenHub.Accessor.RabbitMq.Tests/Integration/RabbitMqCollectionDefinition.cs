using Xunit;

namespace OpenHub.Accessor.RabbitMq.Tests.Integration;

[CollectionDefinition(Name)]
public sealed class RabbitMqCollectionDefinition : ICollectionFixture<RabbitMqCollectionFixture>
{
    public const string Name = "RabbitMq Integration";
}
