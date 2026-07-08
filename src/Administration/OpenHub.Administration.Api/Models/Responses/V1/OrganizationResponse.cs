using System.ComponentModel;

namespace OpenHub.Administration.Api.Models.Responses.V1;

public class OrganizationResponse
{
    [Description("Keycloak organization id.")]
    public string Id { get; set; } = null!;

    [Description("Slug of the Keycloak realm this organization belongs to.")]
    public string RealmSlug { get; set; } = null!;

    [Description("Display name.")]
    public string Name { get; set; } = null!;

    [Description("URL-safe alias, unique within the realm.")]
    public string Alias { get; set; } = null!;

    [Description("Whether the organization is enabled.")]
    public bool Enabled { get; set; }

    [Description("Email domains associated with this organization.")]
    public IReadOnlyList<string> Domains { get; set; } = [];
}
