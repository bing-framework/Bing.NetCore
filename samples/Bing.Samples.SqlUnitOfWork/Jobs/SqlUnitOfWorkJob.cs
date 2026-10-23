using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Data.Sql;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Uow;

namespace Bing.Samples.SqlUnitOfWork.Jobs;

/// <summary>
/// 由 Hangfire Server 创建的 job scope 中执行单个事务场景。
/// </summary>
public sealed class SqlUnitOfWorkJob
{
    private readonly SqlUnitOfWorkApplicationService _service;
    private readonly ScenarioOperationStore _operations;
    private readonly SqlUnitOfWorkSampleOptions _options;
    private readonly IDatabaseContextAccessor _databaseContextAccessor;
    private readonly IBingOperationExecutor? _operationExecutor;

    public SqlUnitOfWorkJob(SqlUnitOfWorkApplicationService service, ScenarioOperationStore operations,
        SqlUnitOfWorkSampleOptions options, IDatabaseContextAccessor databaseContextAccessor,
        IBingOperationExecutor? operationExecutor = null)
    {
        _service = service;
        _operations = operations;
        _options = options;
        _databaseContextAccessor = databaseContextAccessor;
        _operationExecutor = operationExecutor;
    }

    public Task ExecuteAsync(string operationId, string scenario) =>
        ExecuteAsync(operationId, scenario,
            OperationContextSnapshot.Capture(operationId, _databaseContextAccessor), CancellationToken.None);

    public async Task ExecuteAsync(string operationId, string scenario,
        OperationContextSnapshot? context, CancellationToken cancellationToken)
    {
        _operations.MarkRunning(operationId);
        try
        {
            ScenarioExecutionResult result;
            if (_options.OperationsEnabled)
            {
                var executor = _operationExecutor ?? throw new InvalidOperationException(
                    "启用操作模式的宿主未注册 BingOperationExecutor。");
                var snapshot = context ?? OperationContextSnapshot.Capture(operationId, _databaseContextAccessor);
                using (snapshot.Enter(_databaseContextAccessor))
                {
                    result = await executor.ExecuteAsync<OperationScenarioService, ScenarioExecutionResult>(
                        (service, token) => service.ExecuteAsync(scenario, operationId, token),
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                result = await _service.ExecuteAsync(scenario, operationId, cancellationToken);
            }
            _operations.MarkSucceeded(operationId, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _operations.MarkCancelled(operationId);
            throw;
        }
        catch (Exception exception)
        {
            _operations.MarkFailed(operationId, exception);
            throw;
        }
    }
}
