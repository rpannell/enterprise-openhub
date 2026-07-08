using NpgsqlTypes;

namespace OpenHub.ProductManager.Api.Accessors.Models;

public class ProductEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? ConditionTypeId { get; set; }
    public Guid? AvailabilityStatusId { get; set; }
    public Guid? BaseUomId { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsPublished { get; set; }
    public decimal? Weight { get; set; }
    public Guid? WeightUomId { get; set; }
    public Guid CreateUserId { get; set; }
    public Guid ModifyUserId { get; set; }
    public Guid? DeleteUserId { get; set; }
    public NpgsqlRange<DateTimeOffset> SysPeriod { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
