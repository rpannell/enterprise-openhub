using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.KeyCloak.Internal;
using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Accessor.KeyCloak.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OpenHub.Accessor.KeyCloak.Tests;

public class KeyCloakOrganizationAccessorTests
{
    private const string TokenPath = "protocol/openid-connect/token";
    private const string OrgPath = "organizations";

    private static KeyCloakOrganizationAccessor CreateSut(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.test") };
        var options = Options.Create(new KeyCloakOptions
        {
            BaseUrl = "http://keycloak.test",
            AdminUsername = "admin",
            AdminPassword = "secret",
        });
        return new KeyCloakOrganizationAccessor(httpClient, new KeyCloakAdminTokenProvider(options));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage TokenResponse()
        => JsonResponse(HttpStatusCode.OK, """{"access_token":"fake-token","expires_in":300}""");

    private static FakeHttpMessageHandler HandlerWithValidToken()
        => new FakeHttpMessageHandler().When(HttpMethod.Post, TokenPath, _ => TokenResponse());

    [Fact]
    public async Task CreateOrganizationAsync_Success_ReturnsIdFromLocationHeader()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Post, OrgPath, _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Created);
                response.Headers.Location = new Uri("http://keycloak.test/admin/realms/client-c/organizations/org-123");
                return response;
            });
        var sut = CreateSut(handler);

        var id = await sut.CreateOrganizationAsync("client-c", new CreateKeyCloakOrganizationRequest
        {
            Name = "Client C",
            Alias = "client-c",
            Enabled = true,
        });

        id.ShouldBe("org-123");
    }

    [Fact]
    public async Task CreateOrganizationAsync_Conflict_ThrowsKeyCloakAdminConflictException()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Post, OrgPath, _ => new HttpResponseMessage(HttpStatusCode.Conflict));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<KeyCloakAdminConflictException>(() =>
            sut.CreateOrganizationAsync("client-c", new CreateKeyCloakOrganizationRequest { Name = "Client C", Alias = "client-c" }));
    }

    [Fact]
    public async Task GetOrganizationAsync_NotFound_ReturnsNull()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, OrgPath, _ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);

        var result = await sut.GetOrganizationAsync("client-a", "missing-org");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrganizationAsync_Found_ReturnsMappedInfo()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, OrgPath, _ => JsonResponse(HttpStatusCode.OK,
                JsonSerializer.Serialize(new { id = "org-1", name = "Client A", alias = "client-a", enabled = true, domains = new[] { new { name = "client-a.com" } } })));
        var sut = CreateSut(handler);

        var result = await sut.GetOrganizationAsync("client-a", "org-1");

        result.ShouldNotBeNull();
        result.Id.ShouldBe("org-1");
        result.Name.ShouldBe("Client A");
        result.Domains.ShouldContain("client-a.com");
    }

    [Fact]
    public async Task ListOrganizationsAsync_ReturnsAllOrganizations()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, OrgPath, _ => JsonResponse(HttpStatusCode.OK,
                JsonSerializer.Serialize(new[] { new { id = "org-1", name = "Client A", alias = "client-a", enabled = true, domains = Array.Empty<object>() } })));
        var sut = CreateSut(handler);

        var result = await sut.ListOrganizationsAsync("client-a");

        result.Count.ShouldBe(1);
        result[0].Alias.ShouldBe("client-a");
    }

    [Fact]
    public async Task AddMemberAsync_SendsUserIdAsBareJsonStringBody()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Post, "members", _ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = CreateSut(handler);

        await sut.AddMemberAsync("client-a", "org-1", "user-1");

        var memberRequest = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.Contains("members", StringComparison.Ordinal));
        memberRequest.Body.ShouldBe("\"user-1\"");
    }

    [Fact]
    public async Task ListMembersAsync_ReturnsMappedUsers()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, "members", _ => JsonResponse(HttpStatusCode.OK,
                JsonSerializer.Serialize(new[] { new { id = "user-1", username = "alice", email = "alice@client-a.com", firstName = "Alice", lastName = "Smith", enabled = true } })));
        var sut = CreateSut(handler);

        var result = await sut.ListMembersAsync("client-a", "org-1");

        result.Count.ShouldBe(1);
        result[0].Username.ShouldBe("alice");
    }
}
