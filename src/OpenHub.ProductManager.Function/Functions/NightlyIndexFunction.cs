using Microsoft.Azure.Functions.Worker;
using OpenHub.ProductManager.Function.Managers.Interfaces;

namespace OpenHub.ProductManager.Function.Functions;

public class NightlyIndexFunction(INightlyIndexManager manager)
{
    [Function(nameof(NightlyIndexFunction))]
    public async Task Run([TimerTrigger("%NightlyIndexSchedule%")] TimerInfo timer, FunctionContext context)
        => await manager.RunAsync(context.CancellationToken);
}
