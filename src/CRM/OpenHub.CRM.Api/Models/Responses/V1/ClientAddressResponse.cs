using System.ComponentModel;

namespace OpenHub.CRM.Api.Models.Responses.V1;

public class ClientAddressResponse
{
    [Description("Unique address identifier.")]
    public Guid Id { get; set; }

    [Description("Address kind: billing, shipping, headquarters, or other.")]
    public string AddressType { get; set; } = null!;

    [Description("Street address, line 1.")]
    public string Line1 { get; set; } = null!;

    [Description("Street address, line 2.")]
    public string? Line2 { get; set; }

    [Description("City.")]
    public string City { get; set; } = null!;

    [Description("State or province.")]
    public string? StateProvince { get; set; }

    [Description("Postal / ZIP code.")]
    public string? PostalCode { get; set; }

    [Description("ISO 3166-1 alpha-2 country code.")]
    public string CountryCode { get; set; } = null!;

    [Description("Whether this is the client's primary address of its type.")]
    public bool IsPrimary { get; set; }
}
