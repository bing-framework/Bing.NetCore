using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Hosting;

namespace Bing.Admin.Jobs;

/// <summary>
/// 将宿主停止信号转换为 Hangfire 任务排空信号。
/// </summary>
internal sealed class BingHangfireDrainHostedService : IHostedService, IDisposable
{
    /// <summary>
    /// 管理 Hangfire 任务排空状态的运行时对象。
    /// </summary>
    private readonly BingHangfireRuntime _runtime;

    /// <summary>
    /// 拦截任务创建和执行的过滤器。
    /// </summary>
    private readonly BingHangfireDrainFilter _filter;

    /// <summary>
    /// 提供宿主停止信号。
    /// </summary>
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>
    /// 宿主停止回调的注册句柄。
    /// </summary>
    private CancellationTokenRegistration _stoppingRegistration;

    /// <summary>
    /// 指示当前实例是否已释放。
    /// </summary>
    private int _disposed;

    /// <summary>
    /// 初始化一个 <see cref="BingHangfireDrainHostedService" /> 类型的实例。
    /// </summary>
    /// <param name="runtime">任务运行状态。</param>
    /// <param name="filter">任务过滤器。</param>
    /// <param name="lifetime">宿主生命周期。</param>
    public BingHangfireDrainHostedService(BingHangfireRuntime runtime,
        BingHangfireDrainFilter filter, IHostApplicationLifetime lifetime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingRegistration = _lifetime.ApplicationStopping.Register(_runtime.BeginDrain);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _runtime.BeginDrain();
        if (await _runtime.WaitForDrainAsync(Timeout.InfiniteTimeSpan, cancellationToken)
                .ConfigureAwait(false))
            return;

        const string message = "Hangfire 任务停止排空未完成，仍有任务在执行。";
        Exception exception = cancellationToken.IsCancellationRequested
            ? new OperationCanceledException(message, cancellationToken)
            : new TimeoutException(message);
        exception.Data["Bing.Admin.HangfireDrainIncomplete"] = true;
        exception.Data["Bing.Admin.HangfireInFlight"] = _runtime.InFlightCount;
        throw exception;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _stoppingRegistration.Dispose();
        GlobalJobFilters.Filters.Remove(_filter);
    }
}
