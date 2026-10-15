using System.Runtime.ExceptionServices;
using DotNetCore.CAP;
using Microsoft.Extensions.Hosting;

namespace Bing.Events.Cap;

/// <summary>
/// 协调 CAP 启动和宿主停止。
/// </summary>
/// <remarks>
/// CAP 8.0.1 在处理器启动期间取消可能提前释放取消源。
/// 本适配器等待启动结束后再请求停止；超时只结束调用方等待，不提前释放活动资源。
/// 此衔接只等待 BootstrapAsync 和停止方法完成，消息代理就绪及在途业务排空仍需业务层验证。
/// </remarks>
internal sealed class CapHostedLifecycleService : IHostedService, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 当前宿主的 CAP 启动器。
    /// </summary>
    private readonly IBootstrapper _bootstrapper;
    /// <summary>
    /// 可选的单例 CAP 消费宿主排空适配器。
    /// </summary>
    private readonly BingCapConsumerHostDrain _consumerDrain;
    /// <summary>
    /// 启动和关闭状态的同步锁。
    /// </summary>
    private readonly object _gate = new();
    /// <summary>
    /// 本轮启动完成任务。
    /// </summary>
    private Task _startup;
    /// <summary>
    /// 唯一关闭任务。
    /// </summary>
    private Task _shutdown;

    /// <summary>
    /// 初始化一个 <see cref="CapHostedLifecycleService" /> 类型的实例。
    /// </summary>
    public CapHostedLifecycleService(IBootstrapper bootstrapper,
        BingCapConsumerHostDrain consumerDrain = null)
    {
        _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
        _consumerDrain = consumerDrain;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource<bool> completion = null;
        Task startup;
        lock (_gate)
        {
            if (_shutdown != null)
                throw new InvalidOperationException("CAP 已进入关闭流程，不能再次启动。");
            if (_startup == null)
            {
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _startup = completion.Task;
            }
            startup = _startup;
        }
        return StartCoreAsync(startup, completion, cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) =>
        EnsureShutdownAsync().WaitAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => EnsureShutdownAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(EnsureShutdownAsync());

    /// <summary>
    /// 启动 CAP 并等待取消后的清理。
    /// </summary>
    private async Task StartCoreAsync(Task startup, TaskCompletionSource<bool> completion,
        CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => EnsureShutdownAsync());
        if (completion != null)
        {
            try
            {
                // 停止请求由下方唯一关闭任务衔接，不能在原生 Start 尚未结束时取消。
                await _bootstrapper.BootstrapAsync(CancellationToken.None);
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }
        try
        {
            await startup;
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception exception)
        {
            try
            {
                await EnsureShutdownAsync();
            }
            catch (Exception cleanupException)
            {
                exception.Data["Bing.CapStartupCleanupException"] = cleanupException;
            }
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    /// <summary>
    /// 取得本轮唯一关闭任务。
    /// </summary>
    private Task EnsureShutdownAsync()
    {
        lock (_gate)
        {
            var startup = _startup;
            // 原生停止会同步调用处理器 Dispose，独立执行才能让超时只终止等待。
            return _shutdown ??= Task.Run(() => ShutdownCoreAsync(startup));
        }
    }

    /// <summary>
    /// 等待启动结束并关闭 CAP。
    /// </summary>
    private async Task ShutdownCoreAsync(Task startup)
    {
        if (startup != null)
        {
            try
            {
                await startup;
            }
            catch
            {
                // 启动原始异常由 StartCoreAsync 传播；启动失败仍必须执行关闭。
            }
        }
        Exception drainFailure = null;
        Exception stopFailure = null;
        Task stopTask = null;

        if (_consumerDrain != null)
        {
            try
            {
                _consumerDrain.BeginDrain();
            }
            catch (Exception exception)
            {
                drainFailure = exception;
            }
        }

        if (_bootstrapper is IHostedService hostedService)
        {
            try
            {
                // 先停止 CAP 拉取，再等待适配器中的在途业务完成。
                stopTask = hostedService.StopAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                stopFailure = exception;
            }
        }

        if (_consumerDrain != null && drainFailure == null)
        {
            try
            {
                if (!await _consumerDrain.WaitForDrainAsync().ConfigureAwait(false))
                {
                    drainFailure = new TimeoutException("CAP 消费回调未完成排空。");
                    drainFailure.Data["Bing.CapConsumerDrainIncomplete"] = true;
                }
            }
            catch (Exception exception)
            {
                drainFailure = exception;
            }
        }

        if (stopTask != null)
        {
            try
            {
                await stopTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                stopFailure = exception;
            }
        }
        else
        {
            try
            {
                await _bootstrapper.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                stopFailure = exception;
            }
        }

        if (drainFailure != null)
        {
            if (stopFailure != null)
                drainFailure.Data["Bing.CapConsumerStopException"] = stopFailure;
            ExceptionDispatchInfo.Capture(drainFailure).Throw();
        }
        if (stopFailure != null)
            ExceptionDispatchInfo.Capture(stopFailure).Throw();
    }
}
