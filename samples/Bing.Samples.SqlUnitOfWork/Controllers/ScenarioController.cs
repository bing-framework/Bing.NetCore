using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.SqlUnitOfWork.Controllers;

[ApiController]
[Route("api/scenarios")]
public sealed class ScenarioController : ControllerBase
{
    private readonly SqlUnitOfWorkApplicationService _service;
    private readonly ScenarioOperationStore _operations;

    public ScenarioController(SqlUnitOfWorkApplicationService service, ScenarioOperationStore operations)
    {
        _service = service;
        _operations = operations;
    }

    [HttpPost("{scenario}")]
    public async Task<IActionResult> Run(string scenario, string? targetOperationId,
        CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid().ToString("N");
        _operations.Create(operationId, scenario);
        _operations.MarkRunning(operationId);
        try
        {
            var result = await _service.ExecuteAsync(scenario, operationId, cancellationToken, targetOperationId);
            _operations.MarkSucceeded(operationId, result);
            return Ok(result);
        }
        catch (Exception exception)
        {
            _operations.MarkFailed(operationId, exception);
            return Conflict(new ScenarioFailure(operationId, exception.GetType().Name, exception.Message));
        }
    }

    public sealed record ScenarioFailure(string OperationId, string ErrorType, string Error);
}
