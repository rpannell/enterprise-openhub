using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Responses.V1;

public class AvailableKeycloakUserResponse
{
    [Description("Keycloak user id (sub claim).")]
    public string UniqueId { get; set; } = null!;

    [Description("Keycloak username.")]
    public string Username { get; set; } = null!;

    [Description("Email address.")]
    public string? Email { get; set; }

    [Description("First name.")]
    public string? FirstName { get; set; }

    [Description("Last name.")]
    public string? LastName { get; set; }
}
