namespace OpenHub.CRM.Api.Managers.Models;

public class ClientAddressDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string AddressType { get; set; } = null!;
    public string Line1 { get; set; } = null!;
    public string? Line2 { get; set; }
    public string City { get; set; } = null!;
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = null!;
    public bool IsPrimary { get; set; }
}
