using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Uow;
using Bing.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Background;

/// <summary>
/// 为每条内存队列消息创建新的异步依赖注入范围。
/// </summary>
public sealed class ScenarioBackgroundWorker : BackgroundService
{
    private readonly ScenarioWorkQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScenarioOperationStore _operations;
    private readonly SqlUnitOfWorkSampleOptions _options;
    private readonly IDatabaseContextAccessor _databaseContextAccessor;
    private readonly IBingOperationExecutor? _operationExecutor;
    private int _stopped;

    public ScenarioBackgroundWorker(ScenarioWorkQueue queue, IServiceScopeFactory scopeFactory,
        ScenarioOperationStore operations, SqlUnitOfWorkSampleOptions options,
        IDatabaseContextAccessor databaseContextAccessor, IBingOperationExecutor? operationExecutor = null)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _operations = operations;
        _options = options;
        _databaseContextAccessor = databaseContextAccessor;
        _operationExecutor = operationExecutor;
    }

    public bool IsStopped => Volatile.Read(ref _stopped) == 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                _operations.MarkRunning(item.OperationId);
                try
                {
                    ScenarioExecutionResult result;
                    if (_options.OperationsEnabled)
                    {
                        var executor = _operationExecutor ?? throw new InvalidOperationException(
                            "启用操作模式的宿主未注册 BingOperationExecutor。");
                        var context = item.Context ??
                            OperationContextSnapshot.Capture(item.OperationId, _databaseContextAccessor);
                        using (context.Enter(_databaseContextAccessor))
                        {
                            result = await executor.ExecuteAsync<OperationScenarioService, ScenarioExecutionResult>(
                                (service, token) => service.ExecuteAsync(item.Scenario, item.OperationId, token),
                                cancellationToken: stoppingToken);
                        }
                    }
                    else
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var service = scope.ServiceProvider.GetRequiredService<SqlUnitOfWorkApplicationService>();
                        result = await service.ExecuteAsync(item.Scenario, item.OperationId, stoppingToken);
                    }
                    _operations.MarkSucceeded(item.OperationId, result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _operations.MarkCancelled(item.OperationId);
                    break;
                }
                catch (Exception exception)
                {
                    _operations.MarkFailed(item.OperationId, exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _stopped, 1);
        }
    }
}
