namespace OpenHub.ProductManager.ReindexJob.Accessors.Models;

/// <summary>Shape returned by GET /api/v1/products (ProductManager.Api's PagedResponse&lt;T&gt;).</summary>
public class PagedApiResponse<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
