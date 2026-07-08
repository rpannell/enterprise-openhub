using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace OpenHub.Accessor.RabbitMq.Tests.Unit;

[Trait("Category", "Unit")]
public class RabbitMqMessagePublisherGuardClauseTests
{
    private static RabbitMqMessagePublisher CreateSut()
        => new(Options.Create(new RabbitMqPublisherOptions()), Substitute.For<ILogger<RabbitMqMessagePublisher>>());

    [Fact]
    public async Task PublishAsync_MessageIsNull_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentNullException>(() => sut.PublishAsync<string?>(null));
    }

    [Fact]
    public async Task PublishAsync_NotStarted_ThrowsInvalidOperationExceptionWithHostedServiceGuidance()
    {
        var sut = CreateSut();

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => sut.PublishAsync("some message"));

        ex.Message.ShouldContain("AddRabbitMqPublisher");
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RabbitMqMessagePublisher(null!, Substitute.For<ILogger<RabbitMqMessagePublisher>>()));
    }

    [Fact]
    public async Task DisposeAsync_CalledWithoutStart_CompletesWithoutThrowing()
    {
        var sut = CreateSut();

        await sut.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_IsIdempotent()
    {
        var sut = CreateSut();

        await sut.DisposeAsync();
        await sut.DisposeAsync();
    }
}
