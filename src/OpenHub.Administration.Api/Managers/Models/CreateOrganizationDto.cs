namespace OpenHub.Administration.Api.Managers.Models;

public class CreateOrganizationDto
{
    /// <summary>Slug of an already-provisioned realm to create the organization in (see README — realm creation is a manual ops step).</summary>
    public string RealmSlug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Alias { get; set; } = null!;
    public IReadOnlyList<string> Domains { get; set; } = [];
}
