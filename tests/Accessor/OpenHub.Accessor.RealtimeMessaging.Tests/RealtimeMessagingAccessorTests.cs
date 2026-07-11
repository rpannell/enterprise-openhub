using System.Net;
using System.Text.Json;
using OpenHub.Accessor.RealtimeMessaging.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OpenHub.Accessor.RealtimeMessaging.Tests;

public class RealtimeMessagingAccessorTests
{
    private const string MessagesPath = "api/v1/messages";

    private static RealtimeMessagingAccessor CreateSut(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://realtime-messaging-api.test") };
        return new RealtimeMessagingAccessor(httpClient);
    }

    private static FakeHttpMessageHandler HandlerAccepting()
        => new FakeHttpMessageHandler().When(HttpMethod.Post, MessagesPath, _ => new HttpResponseMessage(HttpStatusCode.Accepted));

    [Fact]
    public async Task SendToTenantAsync_PostsToMessagesEndpoint_WithBearerTokenAndTenantSlug()
    {
        var handler = HandlerAccepting();
        var sut = CreateSut(handler);

        await sut.SendToTenantAsync("client-a", "order.updated", "{\"id\":1}", "fake-token", "corr-1");

        var request = handler.Requests.Single();
        request.Authorization!.Scheme.ShouldBe("Bearer");
        request.Authorization!.Parameter.ShouldBe("fake-token");

        using var body = JsonDocument.Parse(request.Body!);
        body.RootElement.GetProperty("TenantSlug").GetString().ShouldBe("client-a");
        body.RootElement.GetProperty("MessageType").GetString().ShouldBe("order.updated");
        body.RootElement.GetProperty("CorrelationId").GetString().ShouldBe("corr-1");
        body.RootElement.TryGetProperty("UserId", out var userId).ShouldBeTrue();
        userId.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SendToUserAsync_PostsWithTenantSlugAndUserId()
    {
        var handler = HandlerAccepting();
        var sut = CreateSut(handler);

        await sut.SendToUserAsync("client-a", "user-1", "notification", "{}", "fake-token");

        var request = handler.Requests.Single();
        using var body = JsonDocument.Parse(request.Body!);
        body.RootElement.GetProperty("TenantSlug").GetString().ShouldBe("client-a");
        body.RootElement.GetProperty("UserId").GetString().ShouldBe("user-1");
    }

    [Fact]
    public async Task SendToAllAsync_PostsWithNoTenantSlugOrUserId()
    {
        var handler = HandlerAccepting();
        var sut = CreateSut(handler);

        await sut.SendToAllAsync("system.maintenance", "{}", "fake-token");

        var request = handler.Requests.Single();
        using var body = JsonDocument.Parse(request.Body!);
        body.RootElement.GetProperty("TenantSlug").ValueKind.ShouldBe(JsonValueKind.Null);
        body.RootElement.GetProperty("UserId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SendToTenantAsync_ObjectPayload_SerializesAsCamelCaseJson()
    {
        var handler = HandlerAccepting();
        var sut = CreateSut(handler);

        await sut.SendToTenantAsync("client-a", "client.updated", new { ClientId = 42, ClientName = "Acme" }, "fake-token");

        var request = handler.Requests.Single();
        using var body = JsonDocument.Parse(request.Body!);
        var payloadJson = body.RootElement.GetProperty("PayloadJson").GetString();
        using var payload = JsonDocument.Parse(payloadJson!);
        payload.RootElement.GetProperty("clientId").GetInt32().ShouldBe(42);
        payload.RootElement.GetProperty("clientName").GetString().ShouldBe("Acme");
    }

    [Fact]
    public async Task SendToTenantAsync_NonSuccessResponse_Throws()
    {
        var handler = new FakeHttpMessageHandler()
            .When(HttpMethod.Post, MessagesPath, _ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<HttpRequestException>(() =>
            sut.SendToTenantAsync("client-a", "order.updated", "{}", "fake-token"));
    }
}
