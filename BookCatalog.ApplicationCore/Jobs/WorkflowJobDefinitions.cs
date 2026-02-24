using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Managers;

namespace BookCatalog.ApplicationCore.Jobs;

public static class WorkflowJobDefinitions
{
    public static async Task TriggerCatalogSyncAsync(ITimeTickerManager<TimeTickerEntity> manager)
    {
        var job = FluentChainTickerBuilder<TimeTickerEntity>
            .BeginWith(parent =>
            {
                parent.SetFunction(nameof(CatalogSyncJob.CatalogSync))
                    .SetExecutionTime(DateTime.UtcNow);
            })
            .WithFirstChild(child =>
            {
                child.SetFunction(nameof(RemainingSyncAuthorsDataSyncJob.AuthorsSync))
                    .SetRunCondition(RunCondition.OnSuccess);
            }).Build();

        await manager.AddAsync(job);
    }
}
