using System.Text.Json.Serialization;
using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak.Internal;

/// <summary>
/// Keycloak's UserRepresentation JSON shape (the fields this library cares about) —
/// shared between <see cref="OpenHub.Accessor.KeyCloak.KeyCloakUserAccessor"/> and
/// <see cref="OpenHub.Accessor.KeyCloak.KeyCloakOrganizationAccessor"/> (organization
/// members are returned as UserRepresentations too), so both map through one type.
/// </summary>
public record KeyCloakUserRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("firstName")] string? FirstName,
    [property: JsonPropertyName("lastName")] string? LastName,
    [property: JsonPropertyName("enabled")] bool Enabled)
{
    public KeyCloakUserInfo ToInfo() => new()
    {
        Id = Id,
        Username = Username,
        Email = Email,
        FirstName = FirstName,
        LastName = LastName,
        Enabled = Enabled
    };
}
