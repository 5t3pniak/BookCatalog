using TickerQ.Utilities.Base;

namespace BookCatalog.ApplicationCore.Jobs;

public class CatalogSyncJob(CatalogSync catalogSync)
{
    [TickerFunction(functionName: nameof(CatalogSync), cronExpression: "0 0 3 * * *")] // every day at 3am
    public async Task CatalogSync(TickerFunctionContext context, CancellationToken cancellationToken)
    {
        await catalogSync.RebuildFromBookDetailsAsync(10,200, cancellationToken);
    }
}