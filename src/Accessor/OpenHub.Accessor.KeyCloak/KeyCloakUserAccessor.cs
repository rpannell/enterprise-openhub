using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Internal;
using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak;

public class KeyCloakUserAccessor(HttpClient http, KeyCloakAdminTokenProvider tokenProvider)
    : IKeyCloakUserAccessor
{
    public async Task<string> CreateUserAsync(
        string realmSlug, CreateKeyCloakUserRequest request, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        var payload = new CreateUserPayload(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            Enabled: true,
            EmailVerified: false,
            Credentials: [new CredentialPayload("password", request.TemporaryPassword, true)],
            RequiredActions: ["UPDATE_PASSWORD"]);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/users")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(httpRequest, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new KeyCloakAdminConflictException(
                $"A user with username '{request.Username}' or email '{request.Email}' already exists in realm '{realmSlug}'.");

        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak did not return a Location header for the created user.");
        return location.Segments[^1];
    }

    public async Task<IReadOnlyList<KeyCloakUserInfo>> ListUsersAsync(
        string realmSlug, string? search = null, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        var path = $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/users";
        if (!string.IsNullOrWhiteSpace(search))
            path += $"?search={Uri.EscapeDataString(search)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<List<KeyCloakUserRepresentation>>(ct) ?? [];
        return json.Select(u => u.ToInfo()).ToList();
    }

    public async Task<KeyCloakUserInfo?> GetUserByIdAsync(
        string realmSlug, string userId, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/users/{Uri.EscapeDataString(userId)}");
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<KeyCloakUserRepresentation>(ct);
        return json?.ToInfo();
    }

    public async Task<IReadOnlyList<KeyCloakRoleInfo>> ListRealmRolesAsync(
        string realmSlug, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/roles");
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<List<RoleResponse>>(ct) ?? [];
        return json.Select(ToInfo).ToList();
    }

    public async Task<KeyCloakRoleInfo?> GetRealmRoleAsync(
        string realmSlug, string roleName, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/roles/{Uri.EscapeDataString(roleName)}");
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<RoleResponse>(ct);
        return json is null ? null : ToInfo(json);
    }

    public async Task AssignRealmRoleAsync(
        string realmSlug, string userId, KeyCloakRoleInfo role, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm")
        {
            Content = JsonContent.Create(new[] { new RoleResponse(role.Id, role.Name, role.Description) })
        };
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveRealmRoleAsync(
        string realmSlug, string userId, KeyCloakRoleInfo role, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm")
        {
            Content = JsonContent.Create(new[] { new RoleResponse(role.Id, role.Name, role.Description) })
        };
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<KeyCloakRoleInfo>> GetUserRealmRolesAsync(
        string realmSlug, string userId, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm");
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<List<RoleResponse>>(ct) ?? [];
        return json.Select(ToInfo).ToList();
    }

    public async Task UpdateUserAsync(
        string realmSlug, string userId, UpdateKeyCloakUserRequest request, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        var payload = new UpdateUserPayload(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Enabled);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/users/{Uri.EscapeDataString(userId)}")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
    }

    private static KeyCloakRoleInfo ToInfo(RoleResponse json) => new()
    {
        Id = json.Id,
        Name = json.Name,
        Description = json.Description
    };

    private record CreateUserPayload(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("lastName")] string? LastName,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("emailVerified")] bool EmailVerified,
        [property: JsonPropertyName("credentials")] CredentialPayload[] Credentials,
        [property: JsonPropertyName("requiredActions")] string[] RequiredActions);

    private record CredentialPayload(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] string Value,
        [property: JsonPropertyName("temporary")] bool Temporary);

    private record UpdateUserPayload(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("lastName")] string? LastName,
        [property: JsonPropertyName("enabled")] bool Enabled);

    private record RoleResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string? Description);
}
