using Xunit;

namespace OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresCollectionFixture>
{
    public const string Name = "Postgres Integration";
}
