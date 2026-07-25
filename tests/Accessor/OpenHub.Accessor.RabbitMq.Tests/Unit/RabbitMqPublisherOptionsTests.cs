using Shouldly;
using Xunit;

namespace OpenHub.Accessor.RabbitMq.Tests.Unit;

[Trait("Category", "Unit")]
public class RabbitMqPublisherOptionsTests
{
    [Fact]
    public void RabbitMqPublisherOptions_DefaultValues_MatchDocumentedDefaults()
    {
        var options = new RabbitMqPublisherOptions();

        options.HostName.ShouldBe("localhost");
        options.Port.ShouldBe(5672);
        options.VirtualHost.ShouldBe("/");
        options.UserName.ShouldBe("guest");
        options.Password.ShouldBe("guest");
        options.DurableExchange.ShouldBeTrue();
        options.PersistentMessages.ShouldBeTrue();
        options.ChannelPoolSize.ShouldBe(10);
        options.AutomaticRecoveryEnabled.ShouldBeTrue();
        options.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(5));
    }
}
