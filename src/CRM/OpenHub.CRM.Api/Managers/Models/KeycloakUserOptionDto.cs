namespace OpenHub.CRM.Api.Managers.Models;

// Backs the "pick an existing Keycloak login" search used when creating a sales rep — nobody
// creating a rep knows a raw Keycloak user id off the top of their head, so the UI searches by
// name/email and this is what it searches over.
public class KeycloakUserOptionDto
{
    public string Id { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }
}
