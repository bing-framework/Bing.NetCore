using Microsoft.Extensions.Hosting;

namespace Bing.Samples.FusionCache;

/// <summary>
/// 由应用拥有的缓存回源工厂排空器。
/// </summary>
/// <remarks>
/// 停止接收新的回源请求，等待已有工厂释放独立作用域后再允许宿主释放容器资源。
/// </remarks>
public sealed class SampleFactoryLifetime : IHostedService
{
    /// <summary>
    /// 保护活动工厂数量和停止状态的同步锁。
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// 所有活动工厂结束后完成的排空信号。
    /// </summary>
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 当前仍在执行的回源工厂数量。
    /// </summary>
    private int _active;

    /// <summary>
    /// 标记宿主是否已经开始排空，用于拒绝新的回源请求。
    /// </summary>
    private bool _stopping;

    /// <summary>
    /// 在排空器尚未停止时执行一次回源工厂。
    /// </summary>
    /// <typeparam name="T">回源结果类型。</typeparam>
    /// <param name="factory">接收取消令牌的异步回源委托。</param>
    /// <param name="token">宿主或调用方提供的取消令牌。</param>
    /// <returns>回源工厂返回的结果。</returns>
    /// <exception cref="InvalidOperationException">宿主已经开始排空时抛出。</exception>
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> factory, CancellationToken token)
    {
        lock (_gate)
        {
            if (_stopping) throw new InvalidOperationException("Host is draining cache factories.");
            _active++;
        }
        return ExecuteAsync(factory, token);
    }

    /// <summary>
    /// 执行回源工厂，并在最后一个工厂结束时完成排空信号。
    /// </summary>
    /// <typeparam name="T">回源结果类型。</typeparam>
    /// <param name="factory">接收取消令牌的异步回源委托。</param>
    /// <param name="token">宿主或调用方提供的取消令牌。</param>
    /// <returns>回源工厂返回的结果。</returns>
    private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> factory, CancellationToken token)
    {
        try { return await factory(token).ConfigureAwait(false); }
        finally
        {
            lock (_gate)
                if (--_active == 0 && _stopping) _drained.TrySetResult();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 排空器无需额外初始化。
    /// </remarks>
    public Task StartAsync(CancellationToken token) => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>
    /// 停止接收新的回源请求并等待全部活动工厂结束；超过宿主关闭预算时报告失败，
    /// 不宣称强行终止了用户代码。
    /// </remarks>
    public Task StopAsync(CancellationToken token)
    {
        lock (_gate)
        {
            _stopping = true;
            if (_active == 0) _drained.TrySetResult();
        }
        // 超过宿主关闭预算就报告失败，不宣称强行终止了用户代码。
        return _drained.Task.WaitAsync(token);
    }
}
