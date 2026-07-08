using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace OpenHub.Accessor.RabbitMq.Tests.Unit;

[Trait("Category", "Unit")]
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddRabbitMqPublisher_FromConfiguration_RegistersOptionsIMessagePublisherAndHostedService()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = "broker.test",
            ["RabbitMq:ExchangeName"] = "custom.exchange",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRabbitMqPublisher(configuration.GetSection("RabbitMq"));
        // RabbitMqMessagePublisher only implements IAsyncDisposable, so the
        // container itself must be disposed asynchronously too.
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RabbitMqPublisherOptions>>().Value;
        options.HostName.ShouldBe("broker.test");
        options.ExchangeName.ShouldBe("custom.exchange");

        var publisher = provider.GetRequiredService<IMessagePublisher>();
        publisher.ShouldBeOfType<RabbitMqMessagePublisher>();

        provider.GetServices<IHostedService>().ShouldContain(s => ReferenceEquals(s, publisher));
    }

    [Fact]
    public async Task AddRabbitMqPublisher_FromAction_RegistersConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRabbitMqPublisher(options => options.ExchangeName = "custom.exchange");
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RabbitMqPublisherOptions>>().Value;

        options.ExchangeName.ShouldBe("custom.exchange");
    }

    [Fact]
    public async Task AddRabbitMqPublisher_IMessagePublisherAndHostedServiceResolveToSameInstance()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRabbitMqPublisher(_ => { });
        await using var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IMessagePublisher>();
        var hostedServices = provider.GetServices<IHostedService>();

        hostedServices.ShouldContain(s => ReferenceEquals(s, publisher));
    }
}
