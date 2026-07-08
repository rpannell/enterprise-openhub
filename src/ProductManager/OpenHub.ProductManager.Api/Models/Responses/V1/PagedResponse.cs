using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Responses.V1;

public class PagedResponse<T>
{
    [Description("Items for the requested page.")]
    public List<T> Items { get; set; } = [];

    [Description("1-based page number returned.")]
    public int Page { get; set; }

    [Description("Rows per page.")]
    public int PageSize { get; set; }

    [Description("Total number of rows matching the filter, across all pages.")]
    public int TotalCount { get; set; }

    [Description("Total number of pages, given PageSize.")]
    public int TotalPages { get; set; }
}
