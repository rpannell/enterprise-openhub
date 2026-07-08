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

public class KeyCloakUserAccessorTests
{
    private const string TokenPath = "protocol/openid-connect/token";
    private const string UsersPath = "users";
    private const string RolesPath = "roles";

    private static KeyCloakUserAccessor CreateSut(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.test") };
        var options = Options.Create(new KeyCloakOptions
        {
            BaseUrl = "http://keycloak.test",
            AdminUsername = "admin",
            AdminPassword = "secret",
        });
        return new KeyCloakUserAccessor(httpClient, new KeyCloakAdminTokenProvider(options));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage TokenResponse()
        => JsonResponse(HttpStatusCode.OK, """{"access_token":"fake-token","expires_in":300}""");

    private static FakeHttpMessageHandler HandlerWithValidToken()
        => new FakeHttpMessageHandler().When(HttpMethod.Post, TokenPath, _ => TokenResponse());

    [Fact]
    public async Task CreateUserAsync_Success_ReturnsIdFromLocationHeader()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Post, UsersPath, _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Created);
                response.Headers.Location = new Uri("http://keycloak.test/admin/realms/client-a/users/user-123");
                return response;
            });
        var sut = CreateSut(handler);

        var id = await sut.CreateUserAsync("client-a", new CreateKeyCloakUserRequest
        {
            Username = "newuser",
            Email = "newuser@client-a.com",
            TemporaryPassword = "Tmp-123!",
        });

        id.ShouldBe("user-123");
    }

    [Fact]
    public async Task CreateUserAsync_Conflict_ThrowsKeyCloakAdminConflictException()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Post, UsersPath, _ => new HttpResponseMessage(HttpStatusCode.Conflict));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<KeyCloakAdminConflictException>(() =>
            sut.CreateUserAsync("client-a", new CreateKeyCloakUserRequest
            {
                Username = "existing",
                Email = "existing@client-a.com",
                TemporaryPassword = "Tmp-123!",
            }));
    }

    [Fact]
    public async Task CreateUserAsync_SendsRequiredUpdatePasswordAction()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Post, UsersPath, _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Created);
                response.Headers.Location = new Uri("http://keycloak.test/admin/realms/client-a/users/user-123");
                return response;
            });
        var sut = CreateSut(handler);

        await sut.CreateUserAsync("client-a", new CreateKeyCloakUserRequest
        {
            Username = "newuser",
            Email = "newuser@client-a.com",
            TemporaryPassword = "Tmp-123!",
        });

        var createRequest = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.EndsWith("/users", StringComparison.Ordinal));
        createRequest.Body.ShouldContain("UPDATE_PASSWORD");
        createRequest.Body.ShouldContain("\"temporary\":true");
    }

    [Fact]
    public async Task GetUserByIdAsync_NotFound_ReturnsNull()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, UsersPath, _ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);

        var result = await sut.GetUserByIdAsync("client-a", "missing-user");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListUsersAsync_ReturnsMappedUsers()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, UsersPath, _ => JsonResponse(HttpStatusCode.OK,
                JsonSerializer.Serialize(new[] { new { id = "user-1", username = "alice", email = "alice@client-a.com", firstName = "Alice", lastName = "Smith", enabled = true } })));
        var sut = CreateSut(handler);

        var result = await sut.ListUsersAsync("client-a");

        result.Count.ShouldBe(1);
        result[0].Username.ShouldBe("alice");
    }

    [Fact]
    public async Task GetRealmRoleAsync_NotFound_ReturnsNull()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RolesPath, _ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);

        var result = await sut.GetRealmRoleAsync("client-a", "nonexistent");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetRealmRoleAsync_Found_ReturnsMappedRole()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RolesPath, _ => JsonResponse(HttpStatusCode.OK,
                JsonSerializer.Serialize(new { id = "role-1", name = "tenant-admin", description = (string?)null })));
        var sut = CreateSut(handler);

        var result = await sut.GetRealmRoleAsync("client-a", "tenant-admin");

        result.ShouldNotBeNull();
        result.Id.ShouldBe("role-1");
        result.Name.ShouldBe("tenant-admin");
    }

    [Fact]
    public async Task AssignRealmRoleAsync_SendsRoleArrayBody()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Post, "role-mappings", _ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = CreateSut(handler);

        await sut.AssignRealmRoleAsync("client-a", "user-1", new KeyCloakRoleInfo { Id = "role-1", Name = "product-manager" });

        var assignRequest = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.Contains("role-mappings", StringComparison.Ordinal));
        assignRequest.Body.ShouldContain("role-1");
        assignRequest.Body.ShouldContain("product-manager");
    }

    [Fact]
    public async Task ListRealmRolesAsync_ReturnsMappedRoles()
    {
        var handler = HandlerWithValidToken()
            .When(HttpMethod.Get, RolesPath, _ => JsonResponse(HttpStatusCode.OK,
                JsonSerializer.Serialize(new[] { new { id = "role-1", name = "user", description = (string?)null } })));
        var sut = CreateSut(handler);

        var result = await sut.ListRealmRolesAsync("client-a");

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("user");
    }
}
