using System.ComponentModel;

namespace OpenHub.CRM.Api.Models.Responses.V1;

public class KeycloakUserOptionResponse
{
    [Description("The Keycloak user id (sub claim) — pass this as KeycloakUserId when creating a sales rep.")]
    public string Id { get; set; } = null!;

    [Description("Display name: the user's full name if Keycloak has one on file, otherwise their username.")]
    public string DisplayName { get; set; } = null!;

    [Description("The user's email address, if Keycloak has one on file.")]
    public string? Email { get; set; }
}
