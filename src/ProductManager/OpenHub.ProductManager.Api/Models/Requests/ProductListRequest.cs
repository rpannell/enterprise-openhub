using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace OpenHub.ProductManager.Api.Models.Requests;

public class ProductListRequest
{
    [Description("1-based page number. Default 1.")]
    [FromQuery]
    public int Page { get; set; } = 1;

    [Description("Rows per page, 1-100. Default 25.")]
    [FromQuery]
    public int PageSize { get; set; } = 25;

    [Description("Case-insensitive search matched against SKU and name.")]
    [FromQuery]
    public string? Search { get; set; }

    [Description("Filter to a single manufacturer owned by the caller's tenant.")]
    [FromQuery]
    public Guid? ManufacturerId { get; set; }

    [Description("Filter to a single brand owned by the caller's tenant.")]
    [FromQuery]
    public Guid? BrandId { get; set; }

    [Description("Filter to a single platform-wide condition type.")]
    [FromQuery]
    public Guid? ConditionTypeId { get; set; }

    [Description("Filter to a single platform-wide availability status.")]
    [FromQuery]
    public Guid? AvailabilityStatusId { get; set; }

    [Description("Filter by the active flag. Omit to include both active and inactive products.")]
    [FromQuery]
    public bool? IsActive { get; set; }

    [Description("Filter by the published flag. Omit to include both published and draft products.")]
    [FromQuery]
    public bool? IsPublished { get; set; }
}
