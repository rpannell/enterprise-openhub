namespace OpenHub.Accessor.KeyCloak.Models;

public class M2MOptions
{
    public const string SectionName = "KeyCloak:M2M";

    public string ClientId { get; set; } = "product-manager-function";

    /// <summary>Client secret per Keycloak realm slug (e.g. "client-a" -> "...").</summary>
    public Dictionary<string, string> ClientSecrets { get; set; } = new();
}
