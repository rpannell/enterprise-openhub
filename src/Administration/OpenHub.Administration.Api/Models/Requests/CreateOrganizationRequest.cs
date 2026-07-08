using System.ComponentModel;

namespace OpenHub.Administration.Api.Models.Requests;

public class CreateOrganizationRequest
{
    [Description("Slug of an already-provisioned Keycloak realm to create the organization in. Realm creation is a manual ops step and is not performed by this endpoint.")]
    public string RealmSlug { get; set; } = null!;

    [Description("Display name of the organization. Max 200 characters.")]
    public string Name { get; set; } = null!;

    [Description("URL-safe alias, unique within the realm. Max 100 characters.")]
    public string Alias { get; set; } = null!;

    [Description("Email domains associated with this organization (e.g. \"acme.com\"). Optional.")]
    public IReadOnlyList<string> Domains { get; set; } = [];
}
