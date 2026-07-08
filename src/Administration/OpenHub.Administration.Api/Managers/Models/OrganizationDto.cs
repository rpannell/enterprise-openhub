namespace OpenHub.Administration.Api.Managers.Models;

public class OrganizationDto
{
    public string Id { get; set; } = null!;
    public string RealmSlug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Alias { get; set; } = null!;
    public bool Enabled { get; set; }
    public IReadOnlyList<string> Domains { get; set; } = [];
}
