using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Requests;

public class UpdateProductRequest
{
    [Description("Updated display name. Omit to leave unchanged. Max 500 characters.")]
    public string? Name { get; set; }

    [Description("Updated full description. Omit to leave unchanged. Max 4000 characters.")]
    public string? Description { get; set; }

    [Description("Updated short description. Omit to leave unchanged. Max 500 characters.")]
    public string? ShortDescription { get; set; }

    [Description("Updated manufacturer reference, owned by the caller's tenant. Omit to leave unchanged. Pass null explicitly to clear.")]
    public Guid? ManufacturerId { get; set; }

    [Description("Updated brand reference, owned by the caller's tenant. Omit to leave unchanged. Pass null explicitly to clear.")]
    public Guid? BrandId { get; set; }

    [Description("Updated condition type reference. Omit to leave unchanged.")]
    public Guid? ConditionTypeId { get; set; }

    [Description("Updated availability status reference. Omit to leave unchanged.")]
    public Guid? AvailabilityStatusId { get; set; }

    [Description("Updated base unit of measure reference. Omit to leave unchanged.")]
    public Guid? BaseUomId { get; set; }

    [Description("Updated manufacturer part number. Omit to leave unchanged.")]
    public string? ManufacturerPartNumber { get; set; }

    [Description("Set to false to deactivate the product and hide it from results. Set to true to reactivate.")]
    public bool? IsActive { get; set; }

    [Description("Set to true to publish the product and make it visible to end customers. Set to false to unpublish.")]
    public bool? IsPublished { get; set; }

    [Description("Updated weight. Must be greater than 0 when provided. Omit to leave unchanged.")]
    public decimal? Weight { get; set; }

    [Description("Updated weight unit of measure reference. Omit to leave unchanged.")]
    public Guid? WeightUomId { get; set; }
}
