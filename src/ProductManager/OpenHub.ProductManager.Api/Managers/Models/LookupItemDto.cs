namespace OpenHub.ProductManager.Api.Managers.Models;

public class LookupItemDto
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
}
