using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenHub.ProductManager.ReindexJob.Managers.Interfaces;

namespace OpenHub.ProductManager.ReindexJob;

/// <summary>
/// Runs the reindex exactly once, then stops the host — this is what makes the process a
/// one-shot job a Kubernetes CronJob (or a plain `docker run`) can invoke and wait on, rather
/// than a long-running service. IHostedService.StartAsync must return promptly (the host
/// considers startup hung otherwise), so the actual run is fire-and-forget from here and
/// awaited internally.
/// </summary>
public class ReindexHostedService(
    IReindexManager manager,
    IHostApplicationLifetime lifetime,
    ILogger<ReindexHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = RunAndStopAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunAndStopAsync(CancellationToken ct)
    {
        try
        {
            var summary = await manager.RunAsync(ct);

            if (summary.AllTenantsSucceeded)
            {
                logger.LogInformation("Reindex completed successfully for all {TenantCount} tenant(s).", summary.TenantCount);
                Environment.ExitCode = 0;
            }
            else
            {
                logger.LogError(
                    "Reindex completed with {FailedCount}/{TenantCount} tenant failure(s): {FailedTenantIds}.",
                    summary.FailedTenantIds.Count, summary.TenantCount, string.Join(", ", summary.FailedTenantIds));
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Reindex job failed with an unhandled exception.");
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
