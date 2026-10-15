using Bing.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.Events.Ordering;

/// <summary>
/// 按配置周期执行有序事件收件箱留存清理。
/// </summary>
/// <remarks>
/// 该服务只删除协调器允许删除的已处理或已归档记录，不触碰待处理或失败记录。
/// 调度默认关闭；停止时会等待当前事务真实完成，不会因调用方等待取消而提前释放作用域。
/// </remarks>
public sealed class OrderedEventRetentionHostedService : IHostedService, IDisposable
{
    /// <summary>
    /// 创建清理作用域的工厂。
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// 清理调度配置。
    /// </summary>
    private readonly OrderedEventRetentionScheduleOptions _options;

    /// <summary>
    /// 记录清理诊断的日志组件。
    /// </summary>
    private readonly ILogger<OrderedEventRetentionHostedService> _logger;

    /// <summary>
    /// 保护启动和停止状态。
    /// </summary>
    private readonly object _gate = new object();

    /// <summary>
    /// 后台清理的取消源。
    /// </summary>
    private CancellationTokenSource _stopping;

    /// <summary>
    /// 后台清理任务。
    /// </summary>
    private Task _runTask;

    /// <summary>
    /// 初始化一个 <see cref="OrderedEventRetentionHostedService" /> 类型的实例。
    /// </summary>
    /// <param name="scopeFactory">创建清理作用域的工厂。</param>
    /// <param name="options">清理调度配置。</param>
    /// <param name="logger">可选的日志组件。</param>
    public OrderedEventRetentionHostedService(IServiceScopeFactory scopeFactory,
        IOptions<OrderedEventRetentionScheduleOptions> options,
        ILogger<OrderedEventRetentionHostedService> logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled)
            return;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            if (scope.ServiceProvider.GetService<OrderedEventCoordinator>() == null)
                throw new InvalidOperationException(
                    "有序事件清理已启用，但未注册 OrderedEventCoordinator。请先注册有序事件存储、事务工厂和协调器。" );
            if (_options.UseCoordinationLease && scope.ServiceProvider.GetService<ILockLeaseProvider>() == null)
                throw new InvalidOperationException(
                    "有序事件清理已启用协调租约，但未注册 ILockLeaseProvider。请注册分布式锁提供程序，或关闭 UseCoordinationLease。" );
        }

        lock (_gate)
        {
            if (_runTask != null)
                throw new InvalidOperationException("有序事件保留清理已经启动。" );
            _stopping = new CancellationTokenSource();
            _runTask = RunAsync(_stopping.Token);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task runTask;
        lock (_gate)
        {
            _stopping?.Cancel();
            runTask = _runTask;
        }
        if (runTask != null)
            await runTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Task runTask;
        lock (_gate)
        {
            _stopping?.Cancel();
            runTask = _runTask;
        }
        if (runTask != null && !runTask.IsCompleted)
            runTask.GetAwaiter().GetResult();
    }

    /// <summary>
    /// 执行周期清理循环。
    /// </summary>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_options.RunImmediately)
                await RunBatchAsync(cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_options.Interval, cancellationToken).ConfigureAwait(false);
                await RunBatchAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                _stopping?.Dispose();
                _stopping = null;
            }
        }
    }

    /// <summary>
    /// 执行一个有界清理批次。
    /// </summary>
    private async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var cutoffUtc = nowUtc.Subtract(_options.RetentionPeriod);
        var archiveCutoffUtc = _options.ArchiveRetentionPeriod.HasValue
            ? nowUtc.Subtract(_options.ArchiveRetentionPeriod.Value)
            : (DateTime?)null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var leaseProvider = _options.UseCoordinationLease
                ? scope.ServiceProvider.GetRequiredService<ILockLeaseProvider>()
                : null;
            if (leaseProvider == null)
            {
                await ExecuteBatchCoreAsync(scope.ServiceProvider, cutoffUtc, archiveCutoffUtc,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var lease = await leaseProvider.TryAcquireLeaseAsync(
                    _options.CoordinationKey,
                    _options.CoordinationWaitTimeout,
                    _options.CoordinationLeaseDuration,
                    cancellationToken)
                .ConfigureAwait(false);
            if (lease == null)
            {
                _logger?.LogDebug("有序事件清理跳过本轮，因为未获取到协调租约。Key={Key}",
                    _options.CoordinationKey);
                return;
            }

            await using (lease)
            {
                await ExecuteBatchCoreAsync(scope.ServiceProvider, cutoffUtc, archiveCutoffUtc,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "有序事件清理失败。CutoffUtc={CutoffUtc}", cutoffUtc);
        }
    }

    /// <summary>
    /// 在已完成调度协调后执行各消费者的清理批次。
    /// </summary>
    private async Task ExecuteBatchCoreAsync(IServiceProvider serviceProvider, DateTime cutoffUtc,
        DateTime? archiveCutoffUtc, CancellationToken cancellationToken)
    {
        var coordinator = serviceProvider.GetRequiredService<OrderedEventCoordinator>();
        Exception firstFailure = null;
        foreach (var consumer in _options.Consumers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var deleted = await coordinator.DeleteProcessedBeforeAsync(consumer, cutoffUtc,
                    _options.BatchSize, cancellationToken).ConfigureAwait(false);
                if (deleted.HasValue)
                {
                    _logger?.LogInformation(
                        "有序事件清理完成。Consumer={Consumer}, Deleted={Deleted}, CutoffUtc={CutoffUtc}",
                        consumer, deleted.Value, cutoffUtc);
                }
                else
                {
                    _logger?.LogWarning(
                        "有序事件清理跳过消费者，因为存储未实现留存契约。Consumer={Consumer}", consumer);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
                _logger?.LogError(exception,
                    "有序事件清理消费者失败。Consumer={Consumer}, CutoffUtc={CutoffUtc}", consumer, cutoffUtc);
            }
        }

        if (archiveCutoffUtc.HasValue)
        {
            foreach (var consumer in _options.ArchiveConsumers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var deleted = await coordinator.DeleteArchivedBeforeAsync(consumer,
                        archiveCutoffUtc.Value, _options.BatchSize, cancellationToken).ConfigureAwait(false);
                    if (deleted.HasValue)
                    {
                        _logger?.LogInformation(
                            "有序事件归档清理完成。Consumer={Consumer}, Deleted={Deleted}, CutoffUtc={CutoffUtc}",
                            consumer, deleted.Value, archiveCutoffUtc.Value);
                    }
                    else
                    {
                        _logger?.LogWarning(
                            "有序事件归档清理跳过消费者，因为存储未实现历史留存契约。Consumer={Consumer}", consumer);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    firstFailure ??= exception;
                    _logger?.LogError(exception,
                        "有序事件归档清理消费者失败。Consumer={Consumer}, CutoffUtc={CutoffUtc}",
                        consumer, archiveCutoffUtc.Value);
                }
            }
        }

        if (firstFailure != null)
            throw firstFailure;
    }
}
