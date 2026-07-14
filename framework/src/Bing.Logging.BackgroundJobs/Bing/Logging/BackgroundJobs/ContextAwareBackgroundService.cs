using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bing.Logging.BackgroundJobs;

/// <summary>
/// 日志上下文感知后台服务
/// </summary>
internal sealed class ContextAwareBackgroundService : BackgroundService
{
    private readonly BackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContextAwareBackgroundService> _logger;

    public ContextAwareBackgroundService(
        BackgroundTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ContextAwareBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            BackgroundTaskQueue.WorkItem item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var serviceScope = _scopeFactory.CreateScope();
                var accessor = serviceScope.ServiceProvider.GetRequiredService<ILogContextAccessor>();
                using (accessor.BeginScope(item.Context))
                    await item.Task(serviceScope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "执行后台队列任务失败。");
            }
        }
    }
}