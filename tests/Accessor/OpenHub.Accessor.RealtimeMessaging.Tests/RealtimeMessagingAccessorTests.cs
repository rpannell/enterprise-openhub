using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OpenHub.Accessor.RealtimeMessaging.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OpenHub.Accessor.RealtimeMessaging.Tests;

[Trait("Category", "Integration")]
public class RealtimeMessagingAccessorTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture _fixture;

    public RealtimeMessagingAccessorTests(RecordingHubFixture fixture)
    {
        _fixture = fixture;
    }

    private RealtimeMessagingAccessor CreateSut()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RealtimeMessagingApi:HubUrl"] = _fixture.HubUrl,
            })
            .Build();
        return new RealtimeMessagingAccessor(configuration);
    }

    [Fact]
    public async Task SendToTenantAsync_InvokesSendToTenantOnHub_WithGivenArguments()
    {
        _fixture.Invocations.Clear();
        var sut = CreateSut();

        await sut.SendToTenantAsync("client-a", "order.updated", "{\"id\":1}", "fake-token", "corr-1");

        var invocation = _fixture.Invocations.ShouldHaveSingleItem();
        invocation.Method.ShouldBe("SendToTenant");
        invocation.Args.ShouldBe(["client-a", "order.updated", "{\"id\":1}", "corr-1"]);
    }

    [Fact]
    public async Task SendToUserAsync_InvokesSendToUserOnHub_WithGivenArguments()
    {
        _fixture.Invocations.Clear();
        var sut = CreateSut();

        await sut.SendToUserAsync("client-a", "user-1", "notification", "{}", "fake-token");

        var invocation = _fixture.Invocations.ShouldHaveSingleItem();
        invocation.Method.ShouldBe("SendToUser");
        invocation.Args.ShouldBe(["client-a", "user-1", "notification", "{}", null]);
    }

    [Fact]
    public async Task BroadcastAsync_InvokesBroadcastOnHub_WithGivenArguments()
    {
        _fixture.Invocations.Clear();
        var sut = CreateSut();

        await sut.BroadcastAsync("system.maintenance", "{}", "fake-token");

        var invocation = _fixture.Invocations.ShouldHaveSingleItem();
        invocation.Method.ShouldBe("Broadcast");
        invocation.Args.ShouldBe(["system.maintenance", "{}", null]);
    }

    [Fact]
    public async Task SendToTenantAsync_ObjectPayload_SerializesAsCamelCaseJson()
    {
        _fixture.Invocations.Clear();
        var sut = CreateSut();

        await sut.SendToTenantAsync("client-a", "client.updated", new { ClientId = 42, ClientName = "Acme" }, "fake-token");

        var invocation = _fixture.Invocations.ShouldHaveSingleItem();
        var payloadJson = (string)invocation.Args[2]!;
        using var payload = JsonDocument.Parse(payloadJson);
        payload.RootElement.GetProperty("clientId").GetInt32().ShouldBe(42);
        payload.RootElement.GetProperty("clientName").GetString().ShouldBe("Acme");
    }
}
