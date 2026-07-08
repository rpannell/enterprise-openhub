using OpenHub.ProductManager.ReindexJob.Managers.Models;

namespace OpenHub.ProductManager.ReindexJob.Managers.Interfaces;

public interface IReindexManager
{
    /// <summary>
    /// Runs one full-catalog reindex to completion: creates a new versioned index, reindexes
    /// every active tenant into it (cutting each over to it as soon as that tenant is done),
    /// and — only if every tenant in this run succeeded — deletes indices left with no
    /// remaining alias reference.
    /// </summary>
    Task<ReindexSummary> RunAsync(CancellationToken ct = default);
}
