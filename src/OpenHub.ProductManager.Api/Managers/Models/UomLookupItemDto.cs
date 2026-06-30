namespace OpenHub.ProductManager.Api.Managers.Models;

public class UomLookupItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
    public string UomType { get; set; } = null!;
}
