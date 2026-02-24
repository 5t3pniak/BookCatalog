using BookCatalog.ApplicationCore.Jobs;
using Microsoft.AspNetCore.Mvc;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace BookCatalog.Api.Internal;

[ApiController]
[Route("internal/api/sync")]
public class SyncTriggerController(ITimeTickerManager<TimeTickerEntity> manager) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> TriggerSync(CancellationToken ct = default)
    {
        await WorkflowJobDefinitions.TriggerCatalogSyncAsync(manager);
        return Accepted();
    }
}
