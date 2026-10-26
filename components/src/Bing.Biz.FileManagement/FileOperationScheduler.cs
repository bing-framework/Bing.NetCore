using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.Biz.FileManagement;

/// <summary>
/// 按长短任务分配固定执行槽，并独立运行维护循环。限制按宿主实例生效，跨进程所有权由数据库租约保证。
/// 每次执行使用独立作用域，不共享请求中的服务或在内存中预领取任务。
/// </summary>
public sealed class FileOperationScheduler
{
    private readonly IServiceScopeFactory _scopes;
    private readonly FileManagementOptions _options;
    private readonly ILogger<FileOperationScheduler> _logger;
    private int _running;
    private long _lastMaintenanceAt;
    public bool IsRunning => Volatile.Read(ref _running) != 0;
    public long LastMaintenanceAt => Interlocked.Read(ref _lastMaintenanceAt);

    public FileOperationScheduler(IServiceScopeFactory scopes, FileManagementOptions options, ILogger<FileOperationScheduler> logger)
    { _scopes = scopes; _options = options; _logger = logger; }

    /// <summary>运行到取消，并等待所有执行槽及作用域完成收尾；同一实例不能重复启动。</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _options.ValidateScheduling();
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw new InvalidOperationException("文件任务调度器已经运行。");
        try
        {
            // 启动时固定槽数量与周期，运行期间不随可变 options 隐式扩容。
            var pollInterval = _options.OperationPollInterval;
            var tasks = new List<Task>();
            for (var i = 0; i < _options.ShortOperationConcurrency; i++)
                tasks.Add(RunOperationsAsync(FileOperationQueue.Short, pollInterval, cancellationToken));
            for (var i = 0; i < _options.LongOperationConcurrency; i++)
                tasks.Add(RunOperationsAsync(FileOperationQueue.Long, pollInterval, cancellationToken));
            tasks.Add(RunMaintenanceAsync(_options.CleanupInterval, cancellationToken));
            tasks.Add(RunEnhancementsAsync(pollInterval, cancellationToken));
            await Task.WhenAll(tasks);
        }
        finally { Volatile.Write(ref _running, 0); }
    }

    private async Task RunOperationsAsync(FileOperationQueue queue, TimeSpan pollInterval, CancellationToken ct)
    {
        // SQLite 等 Provider 可能同步完成异步方法，先让全部槽启动，避免第一个槽独占启动过程。
        await Task.Yield();
        while (!ct.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                processed = await scope.ServiceProvider.GetRequiredService<FileOperationProcessor>().RunNextAsync(queue, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // 未领取或提交结果未知都交给持久状态重试；不输出可能包含签名的异常消息。
                _logger.LogError("文件 {Queue} 队列调度失败：{ErrorType}", queue, ex.GetType().Name);
            }
            if (!processed)
            {
                if (!await DelayAsync(pollInterval, ct)) return;
            }
            else await Task.Yield();
        }
    }

    private async Task RunMaintenanceAsync(TimeSpan interval, CancellationToken ct)
    {
        await Task.Yield();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<FileOperationProcessor>().RunMaintenanceAsync(ct);
                var snapshot = await scope.ServiceProvider.GetRequiredService<IFileManagementStore>().GetOperationalSnapshotAsync(ct);
                scope.ServiceProvider.GetRequiredService<FileOperationalMetrics>().Update(snapshot);
                Interlocked.Exchange(ref _lastMaintenanceAt, FileManagementService.Now);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { _logger.LogError("文件任务维护失败：{ErrorType}", ex.GetType().Name); }
            if (!await DelayAsync(interval, ct)) return;
        }
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
    }

    private async Task RunEnhancementsAsync(TimeSpan interval, CancellationToken ct)
    {
        await Task.Yield();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<FileOperationProcessor>().RunEnhancementsAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { _logger.LogError("文件增强维护失败：{ErrorType}", ex.GetType().Name); }
            if (!await DelayAsync(interval, ct)) return;
        }
    }
}
