using NSubstitute;
using Shouldly;
using Xunit;

namespace OpenHub.Accessor.RabbitMq.Tests.Unit;

[Trait("Category", "Unit")]
public class MessagePublisherExtensionsTests
{
    private sealed class TestEventOptions : EntityEventOptions;

    private sealed record TestMessage(string Value);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PublishEntityEventAsync_ExchangeNameNullOrWhitespace_DoesNotPublish(string? exchangeName)
    {
        var publisher = Substitute.For<IMessagePublisher>();
        var options = new TestEventOptions { ExchangeName = exchangeName };

        await publisher.PublishEntityEventAsync(options, new TestMessage("hello"));

        await publisher.DidNotReceive().PublishAsync(
            Arg.Any<TestMessage>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishEntityEventAsync_ExchangeNameConfigured_PublishesToThatExchange()
    {
        var publisher = Substitute.For<IMessagePublisher>();
        var options = new TestEventOptions { ExchangeName = "some.exchange" };
        var message = new TestMessage("hello");
        var ct = new CancellationTokenSource().Token;

        await publisher.PublishEntityEventAsync(options, message, ct);

        await publisher.Received(1).PublishAsync(message, "some.exchange", routingKey: null, cancellationToken: ct);
    }

    [Fact]
    public void PublishEntityEventAsync_NullPublisher_ThrowsArgumentNullException()
    {
        IMessagePublisher publisher = null!;
        var options = new TestEventOptions { ExchangeName = "some.exchange" };

        Should.Throw<ArgumentNullException>(() => publisher.PublishEntityEventAsync(options, new TestMessage("hello")));
    }

    [Fact]
    public void PublishEntityEventAsync_NullOptions_ThrowsArgumentNullException()
    {
        var publisher = Substitute.For<IMessagePublisher>();

        Should.Throw<ArgumentNullException>(() => publisher.PublishEntityEventAsync(null!, new TestMessage("hello")));
    }
}
