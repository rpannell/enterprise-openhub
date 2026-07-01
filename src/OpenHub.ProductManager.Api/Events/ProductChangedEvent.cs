namespace OpenHub.ProductManager.Api.Events;

public sealed class ProductChangedEvent
{
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool IsDeleted { get; set; } = false;
}
