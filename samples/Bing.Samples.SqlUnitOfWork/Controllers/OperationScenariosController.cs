using Bing.Data.Sql;
using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Uow;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.SqlUnitOfWork.Controllers;

[ApiController]
[Route("api/operation-scenarios")]
public sealed class OperationScenariosController : ControllerBase
{
    private readonly IBingOperationExecutor? _executor;
    private readonly IDatabaseContextAccessor _databaseContextAccessor;
    private readonly SqlUnitOfWorkSampleOptions _options;
    private readonly ScenarioOperationStore _operations;

    public OperationScenariosController(IDatabaseContextAccessor databaseContextAccessor,
        SqlUnitOfWorkSampleOptions options, ScenarioOperationStore operations,
        IBingOperationExecutor? executor = null)
    {
        _executor = executor;
        _databaseContextAccessor = databaseContextAccessor;
        _options = options;
        _operations = operations;
    }

    [HttpPost("{scenario}")]
    public async Task<IActionResult> Run(string scenario, CancellationToken cancellationToken)
    {
        if (!_options.OperationsEnabled)
            return NotFound();

        var operationId = Guid.NewGuid().ToString("N");
        var context = OperationContextSnapshot.Capture(operationId, _databaseContextAccessor);
        _operations.Create(operationId, scenario);
        _operations.MarkRunning(operationId);
        try
        {
            _operations.RecordProcessStep(operationId, operationId, "operation", "context-enter-started");
            using (context.Enter(_databaseContextAccessor))
            {
                _operations.RecordProcessStep(operationId, operationId, "operation", "context-entered");
                var executor = _executor ?? throw new InvalidOperationException(
                    "操作执行入口未注册 BingOperationExecutor。");
                var execution = executor.ExecuteAsync<OperationScenarioService, ScenarioExecutionResult>(
                    (service, token) => service.ExecuteAsync(scenario, operationId, token),
                    cancellationToken: cancellationToken);
                _operations.RecordProcessStep(operationId, operationId, "operation", "executor-task-returned");
                var result = await execution;
                _operations.MarkSucceeded(operationId, result);
                return Ok(result);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _operations.MarkCancelled(operationId);
            throw;
        }
        catch (Exception exception)
        {
            _operations.MarkFailed(operationId, exception);
            return Conflict(new ScenarioController.ScenarioFailure(operationId,
                exception.GetType().Name, exception.Message));
        }
    }
}
