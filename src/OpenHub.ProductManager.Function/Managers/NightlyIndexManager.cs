using Microsoft.Extensions.Logging;
using OpenHub.ProductManager.Function.Managers.Interfaces;

namespace OpenHub.ProductManager.Function.Managers;

public class NightlyIndexManager(ILogger<NightlyIndexManager> logger) : INightlyIndexManager
{
    // Scaffold only — tenant enumeration (which tenants/realms exist) and the
    // Elastic full-reindex write both need their own design pass before this
    // does real work. See project plan notes for the open questions.
    public Task RunAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Nightly product index run started — not yet implemented.");
        return Task.CompletedTask;
    }
}
