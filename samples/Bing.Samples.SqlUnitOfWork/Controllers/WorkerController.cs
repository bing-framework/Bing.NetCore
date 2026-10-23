using Bing.Samples.SqlUnitOfWork.Infrastructure.Background;
using Bing.Data.Sql;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.SqlUnitOfWork.Controllers;

[ApiController]
[Route("api/worker/scenarios")]
public sealed class WorkerController : ControllerBase
{
    private readonly ScenarioWorkQueue _queue;
    private readonly ScenarioOperationStore _operations;
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    public WorkerController(ScenarioWorkQueue queue, ScenarioOperationStore operations,
        IDatabaseContextAccessor databaseContextAccessor)
    {
        _queue = queue;
        _operations = operations;
        _databaseContextAccessor = databaseContextAccessor;
    }

    [HttpPost("{scenario}")]
    public async Task<IActionResult> Enqueue(string scenario, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid().ToString("N");
        _operations.Create(operationId, scenario);
        try
        {
            await _queue.EnqueueAsync(new ScenarioWorkItem(operationId, scenario)
            {
                Context = OperationContextSnapshot.Capture(operationId, _databaseContextAccessor)
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _operations.MarkFailed(operationId, exception);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { operationId, error = exception.Message });
        }

        return Accepted($"/api/operations/{operationId}", new { operationId, status = "Queued" });
    }
}
