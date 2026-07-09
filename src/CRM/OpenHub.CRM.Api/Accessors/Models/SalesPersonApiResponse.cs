namespace OpenHub.CRM.Api.Accessors.Models;

/// <summary>
/// Thin mirror of SalesOrg.Api's SalesPersonResponse — only the fields CRM actually needs
/// (existence check + display name), so this accessor doesn't have to track SalesOrg's full
/// response shape.
/// </summary>
public class SalesPersonApiResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
}
