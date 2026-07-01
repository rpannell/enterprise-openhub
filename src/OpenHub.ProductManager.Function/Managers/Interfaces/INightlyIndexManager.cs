namespace OpenHub.ProductManager.Function.Managers.Interfaces;

public interface INightlyIndexManager
{
    Task RunAsync(CancellationToken ct = default);
}
