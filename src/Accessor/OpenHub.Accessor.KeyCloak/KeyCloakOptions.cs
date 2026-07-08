namespace OpenHub.Accessor.KeyCloak;

public class KeyCloakOptions
{
    public const string SectionName = "KeyCloak";

    public string BaseUrl { get; set; } = null!;
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = null!;
}
