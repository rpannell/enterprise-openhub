using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.Function.Managers.Interfaces;

namespace OpenHub.ProductManager.Function.Functions;

public class ProductChangedQueueFunction(IProductSyncManager manager)
{
    [Function(nameof(ProductChangedQueueFunction))]
    public async Task Run(
        [RabbitMQTrigger("product-manager-function.product-changed", ConnectionStringSetting = "RabbitMQConnection")] string item,
        FunctionContext context)
    {
        var evt = JsonSerializer.Deserialize<ProductChangedEvent>(item)
            ?? throw new InvalidOperationException("Received an empty/invalid ProductChangedEvent.");

        await manager.HandleAsync(evt, context.CancellationToken);
    }
}
