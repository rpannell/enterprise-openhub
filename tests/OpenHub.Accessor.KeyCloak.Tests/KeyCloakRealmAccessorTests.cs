using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.KeyCloak.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OpenHub.Accessor.KeyCloak.Tests;

public class KeyCloakRealmAccessorTests
{
    private const string TokenPath = "protocol/openid-connect/token";
    private const string RealmPath = "admin/realms";

    private static KeyCloakRealmAccessor CreateSut(FakeHttpMessageHandler handler, string adminUsername = "admin", string adminPassword = "secret")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.test") };
        var options = Options.Create(new KeyCloakOptions
        {
            BaseUrl = "http://keycloak.test",
            AdminUsername = adminUsername,
            AdminPassword = adminPassword,
        });
        return new KeyCloakRealmAccessor(httpClient, options);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage TokenResponse(string accessToken = "fake-token-abc", int expiresIn = 300)
        => JsonResponse(HttpStatusCode.OK, $$"""{"access_token":"{{accessToken}}","expires_in":{{expiresIn}}}""");

    private static HttpResponseMessage RealmResponse(string id = "realm-id-1", string realm = "acme", string? displayName = "Acme Corp", bool enabled = true)
        => JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new { id, realm, displayName, enabled }));

    private static FakeHttpMessageHandler HandlerWithValidToken()
        => new FakeHttpMessageHandler().When(HttpMethod.Post, TokenPath, _ => TokenResponse());

    [Fact]
    public async Task GetRealmBySlugAsync_RealmExists_ReturnsMappedRealmInfo()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => RealmResponse("realm-id-1", "acme", "Acme Corp", true));
        var sut = CreateSut(handler);

        var result = await sut.GetRealmBySlugAsync("acme");

        result.ShouldNotBeNull();
        result.Id.ShouldBe("realm-id-1");
        result.Realm.ShouldBe("acme");
        result.DisplayName.ShouldBe("Acme Corp");
        result.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetRealmBySlugAsync_RealmNotFound_ReturnsNull()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);

        var result = await sut.GetRealmBySlugAsync("missing-realm");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetRealmBySlugAsync_ServerError_ThrowsHttpRequestException()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<HttpRequestException>(() => sut.GetRealmBySlugAsync("acme"));
    }

    [Fact]
    public async Task GetRealmBySlugAsync_Unauthorized_ThrowsHttpRequestException()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<HttpRequestException>(() => sut.GetRealmBySlugAsync("acme"));
    }

    [Fact]
    public async Task GetRealmBySlugAsync_MalformedJson_ThrowsJsonException()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => JsonResponse(HttpStatusCode.OK, "{not valid json"));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<JsonException>(() => sut.GetRealmBySlugAsync("acme"));
    }

    [Fact]
    public async Task GetRealmBySlugAsync_EmptyBody_ThrowsJsonException()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => JsonResponse(HttpStatusCode.OK, ""));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<JsonException>(() => sut.GetRealmBySlugAsync("acme"));
    }

    [Fact]
    public async Task GetRealmBySlugAsync_NullJsonLiteral_ReturnsNull()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => JsonResponse(HttpStatusCode.OK, "null"));
        var sut = CreateSut(handler);

        var result = await sut.GetRealmBySlugAsync("acme");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetRealmBySlugAsync_DisplayNameWhitespace_NormalizesToNull()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => RealmResponse(displayName: "   "));
        var sut = CreateSut(handler);

        var result = await sut.GetRealmBySlugAsync("acme");

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBeNull();
    }

    [Fact]
    public async Task GetRealmBySlugAsync_DisplayNamePresent_PreservesValue()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => RealmResponse(displayName: "Acme Corp"));
        var sut = CreateSut(handler);

        var result = await sut.GetRealmBySlugAsync("acme");

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe("Acme Corp");
    }

    [Fact]
    public async Task GetRealmBySlugAsync_SlugContainsSpecialCharacters_UriEscapesSlugInRequestPath()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => RealmResponse());
        var sut = CreateSut(handler);

        await sut.GetRealmBySlugAsync("acme corp/prod");

        var realmRequest = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.Contains(RealmPath, StringComparison.Ordinal));
        var escapedSlug = Uri.EscapeDataString("acme corp/prod");
        realmRequest.RequestUri!.AbsolutePath.ShouldBe($"/admin/realms/{escapedSlug}");
        realmRequest.RequestUri.AbsolutePath.ShouldNotContain("acme corp/prod");
    }

    [Fact]
    public async Task GetRealmBySlugAsync_ValidToken_SendsBearerAuthorizationHeader()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => RealmResponse());
        var sut = CreateSut(handler);

        await sut.GetRealmBySlugAsync("acme");

        var realmRequest = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.Contains(RealmPath, StringComparison.Ordinal));
        realmRequest.Authorization.ShouldNotBeNull();
        realmRequest.Authorization!.Scheme.ShouldBe("Bearer");
        realmRequest.Authorization.Parameter.ShouldBe("fake-token-abc");
    }

    [Fact]
    public async Task GetRealmBySlugAsync_CalledTwiceWithinTokenTtl_FetchesTokenOnlyOnce()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RealmPath, _ => RealmResponse());
        var sut = CreateSut(handler);

        await sut.GetRealmBySlugAsync("acme");
        await sut.GetRealmBySlugAsync("beta");

        handler.RequestCountTo(TokenPath).ShouldBe(1);
        handler.RequestCountTo(RealmPath).ShouldBe(2);
    }

    [Fact]
    public async Task GetRealmBySlugAsync_TokenExpired_FetchesNewTokenOnNextCall()
    {
        // expires_in: 1, minus the source's 30s safety margin, is already in the
        // past the moment it's computed — no Thread.Sleep/time mocking needed.
        var handler = new FakeHttpMessageHandler()
            .When(HttpMethod.Post, TokenPath, _ => TokenResponse(expiresIn: 1))
            .When(HttpMethod.Get, RealmPath, _ => RealmResponse());
        var sut = CreateSut(handler);

        await sut.GetRealmBySlugAsync("acme");
        await sut.GetRealmBySlugAsync("beta");

        handler.RequestCountTo(TokenPath).ShouldBe(2);
    }

    [Fact]
    public async Task GetRealmBySlugAsync_ConcurrentCallsWithNoCachedToken_FetchesTokenOnlyOnce()
    {
        var gate = new TaskCompletionSource();
        var handler = new FakeHttpMessageHandler()
            .WhenAsync(HttpMethod.Post, TokenPath, async (_, ct) =>
            {
                await gate.Task.WaitAsync(ct);
                return TokenResponse();
            })
            .When(HttpMethod.Get, RealmPath, _ => RealmResponse());
        var sut = CreateSut(handler);

        var calls = Enumerable.Range(0, 5).Select(_ => sut.GetRealmBySlugAsync("acme")).ToArray();
        // Give the concurrent calls a moment to all reach the gated token request
        // before releasing it, so they genuinely overlap.
        await Task.Delay(50);
        gate.SetResult();
        await Task.WhenAll(calls);

        handler.RequestCountTo(TokenPath).ShouldBe(1);
    }

    [Fact]
    public async Task GetRealmBySlugAsync_TokenEndpointReturnsEmptyBody_ThrowsJsonException()
    {
        var handler = new FakeHttpMessageHandler()
            .When(HttpMethod.Post, TokenPath, _ => JsonResponse(HttpStatusCode.OK, ""));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<JsonException>(() => sut.GetRealmBySlugAsync("acme"));
    }

    [Fact]
    public async Task GetRealmBySlugAsync_TokenEndpointReturnsErrorStatus_ThrowsAndNeverCallsRealmEndpoint()
    {
        var handler = new FakeHttpMessageHandler()
            .When(HttpMethod.Post, TokenPath, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized))
            .When(HttpMethod.Get, RealmPath, _ => RealmResponse());
        var sut = CreateSut(handler);

        await Should.ThrowAsync<HttpRequestException>(() => sut.GetRealmBySlugAsync("acme"));

        handler.RequestCountTo(RealmPath).ShouldBe(0);
    }
}
