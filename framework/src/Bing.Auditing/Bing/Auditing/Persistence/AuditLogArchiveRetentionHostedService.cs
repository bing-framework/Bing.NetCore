using Bing.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 按配置周期执行经过授权的审计归档历史清理。
/// </summary>
/// <remarks>
/// 该服务默认关闭，并且每轮只执行一个有界批次。启用前必须确认归档历史已经超过业务重放窗口，
/// 完成外部备份并由宿主提供受信任的授权器。停止时会等待当前存储调用实际结束，不会提前释放存储资源。
/// </remarks>
public sealed class AuditLogArchiveRetentionHostedService : IHostedService, IDisposable
{
    /// <summary>
    /// 创建清理作用域的工厂。
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// 清理调度配置。
    /// </summary>
    private readonly AuditLogArchiveRetentionScheduleOptions _options;

    /// <summary>
    /// 记录后台清理诊断的日志组件。
    /// </summary>
    private readonly ILogger<AuditLogArchiveRetentionHostedService> _logger;

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
    /// 初始化一个 <see cref="AuditLogArchiveRetentionHostedService" /> 类型的实例。
    /// </summary>
    /// <param name="scopeFactory">创建清理作用域的工厂。</param>
    /// <param name="options">清理调度配置。</param>
    /// <param name="logger">可选的日志组件。</param>
    public AuditLogArchiveRetentionHostedService(IServiceScopeFactory scopeFactory,
        IOptions<AuditLogArchiveRetentionScheduleOptions> options,
        ILogger<AuditLogArchiveRetentionHostedService> logger = null)
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
            if (scope.ServiceProvider.GetService<IAuditLogArchiveRetentionService>() == null)
                throw new InvalidOperationException(
                    "审计归档历史清理已启用，但未注册 IAuditLogArchiveRetentionService。请注册归档历史清理服务后再启动。" );
            if (_options.UseCoordinationLease && scope.ServiceProvider.GetService<ILockLeaseProvider>() == null)
                throw new InvalidOperationException(
                    "审计归档历史清理已启用协调租约，但未注册 ILockLeaseProvider。请注册分布式锁提供程序，或关闭 UseCoordinationLease。");
        }

        lock (_gate)
        {
            if (_runTask != null)
                throw new InvalidOperationException("审计归档历史清理已经启动。");
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
        var cutoffUtc = DateTime.UtcNow.Subtract(_options.RetentionPeriod);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var serviceProvider = scope.ServiceProvider;
            if (!_options.UseCoordinationLease)
            {
                await ExecuteBatchCoreAsync(serviceProvider, cutoffUtc, cancellationToken).ConfigureAwait(false);
                return;
            }

            var leaseProvider = serviceProvider.GetRequiredService<ILockLeaseProvider>();
            var lease = await leaseProvider.TryAcquireLeaseAsync(
                    _options.CoordinationKey,
                    _options.CoordinationWaitTimeout,
                    _options.CoordinationLeaseDuration,
                    cancellationToken)
                .ConfigureAwait(false);
            if (lease == null)
            {
                _logger?.LogDebug("审计归档历史清理跳过本轮，因为未获取到协调租约。Key={Key}",
                    _options.CoordinationKey);
                return;
            }

            await using (lease)
            {
                await ExecuteBatchCoreAsync(serviceProvider, cutoffUtc, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "审计归档历史清理失败。CutoffUtc={CutoffUtc}", cutoffUtc);
        }
    }

    /// <summary>
    /// 在已完成调度协调后执行一个清理批次。
    /// </summary>
    private async Task ExecuteBatchCoreAsync(IServiceProvider serviceProvider, DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        var service = serviceProvider.GetRequiredService<IAuditLogArchiveRetentionService>();
        var deleted = await service.DeleteBeforeAsync(cutoffUtc, _options.BatchSize, cancellationToken)
            .ConfigureAwait(false);
        _logger?.LogInformation("审计归档历史清理完成。Deleted={Deleted}, CutoffUtc={CutoffUtc}", deleted, cutoffUtc);
    }
}
