using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Responses.V1;

public class UomLookupItemResponse
{
    [Description("Unique identifier. Use this value when referencing the unit of measure from a request.")]
    public Guid Id { get; set; }

    [Description("Short machine-readable code (e.g. KG, EA).")]
    public string Code { get; set; } = null!;

    [Description("Display name (e.g. Kilogram, Each).")]
    public string Name { get; set; } = null!;

    [Description("Display abbreviation (e.g. kg, ea).")]
    public string Abbreviation { get; set; } = null!;

    [Description("Unit category: WEIGHT, LENGTH, VOLUME, QUANTITY, or AREA. Use to filter applicable units for a given field.")]
    public string UomType { get; set; } = null!;
}
