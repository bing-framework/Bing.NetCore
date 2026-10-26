using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Bing.Caching.FusionCache.Internal;

/// <summary>
/// 为实际执行的回源工厂申请并发与排队额度，并发布准入诊断。
/// </summary>
/// <remarks>
/// 超时返回不会提前释放仍在运行的工厂额度；递归或嵌套冷回源会在申请额度前被拒绝。
/// </remarks>
internal sealed class FusionCacheFactoryInvoker
{
    /// <summary>
    /// Bing 混合缓存诊断使用的 Meter 与 ActivitySource 名称。
    /// </summary>
    internal const string DiagnosticName = "Bing.Caching.FusionCache";

    /// <summary>
    /// 发布 Bing 回源准入指标的共享 Meter。
    /// </summary>
    private static readonly Meter Meter = new(DiagnosticName);

    /// <summary>
    /// 创建 Bing 回源 Activity 的共享 ActivitySource。
    /// </summary>
    private static readonly ActivitySource Activities = new(DiagnosticName);

    /// <summary>
    /// 当前正在等待回源许可的工厂数量。
    /// </summary>
    private static readonly UpDownCounter<long> Waiting = Meter.CreateUpDownCounter<long>("bing.cache.factory.waiting");

    /// <summary>
    /// 当前正在执行的回源工厂数量。
    /// </summary>
    private static readonly UpDownCounter<long> Active = Meter.CreateUpDownCounter<long>("bing.cache.factory.active");

    /// <summary>
    /// 因取消、超时或队列已满而被拒绝的回源工厂数量。
    /// </summary>
    private static readonly Counter<long> Rejected = Meter.CreateCounter<long>("bing.cache.factory.rejected");

    /// <summary>
    /// 回源工厂等待许可的耗时分布，单位为毫秒。
    /// </summary>
    private static readonly Histogram<double> WaitDuration = Meter.CreateHistogram<double>("bing.cache.factory.wait.duration", "ms");

    /// <summary>
    /// 当前异步执行上下文中的回源调用链，用于检测递归与嵌套冷回源。
    /// </summary>
    private static readonly AsyncLocal<Frame?> Current = new();

    /// <summary>
    /// 当前缓存实例的并发与排队额度配置。
    /// </summary>
    private readonly CacheResourceOptions _resources;

    /// <summary>
    /// 等待回源许可的最长时间。
    /// </summary>
    private readonly TimeSpan _queueTimeout;

    /// <summary>
    /// 当前缓存实例名称，用于诊断标签和日志。
    /// </summary>
    private readonly string _name;

    /// <summary>
    /// 记录回源准入失败的可选日志记录器。
    /// </summary>
    private readonly ILogger? _logger;

    /// <summary>
    /// 初始化一个 <see cref="FusionCacheFactoryInvoker" /> 类的新实例。
    /// </summary>
    /// <param name="name">缓存实例名称。</param>
    /// <param name="options">提供回源并发、排队与超时预算的配置。</param>
    /// <param name="logger">记录准入失败的可选日志记录器。</param>
    internal FusionCacheFactoryInvoker(string name, BingFusionCacheOptions options, ILogger? logger)
    {
        _name = name;
        _resources = new CacheResourceOptions(options.MaxConcurrentFactories, options.MaxQueuedFactories);
        _queueTimeout = options.FactoryQueueTimeout;
        _logger = logger;
    }

    /// <summary>
    /// 检测当前异步调用链中是否存在同键回源。
    /// </summary>
    /// <param name="key">规范化后的原生缓存键。</param>
    /// <exception cref="InvalidOperationException">同一实例中检测到同键递归回源时抛出。</exception>
    internal void CheckRecursion(string key)
    {
        for (var frame = Current.Value; frame != null; frame = frame.Parent)
            if (frame.Owner == this && frame.Key == key)
                throw new InvalidOperationException("不允许同键递归缓存回源。");
    }

    /// <summary>
    /// 申请回源额度并执行回源工厂。
    /// </summary>
    /// <typeparam name="T">回源结果类型。</typeparam>
    /// <param name="key">规范化后的原生缓存键，用于递归检测与诊断。</param>
    /// <param name="factory">接收缓存取消令牌的异步回源委托。</param>
    /// <param name="token">回源工厂的取消令牌。</param>
    /// <returns>回源工厂返回的结果。</returns>
    /// <exception cref="InvalidOperationException">同一实例中存在嵌套冷回源或额度配置无效时抛出。</exception>
    /// <exception cref="TimeoutException">等待回源许可超时时抛出。</exception>
    /// <exception cref="OperationCanceledException">等待许可或工厂执行期间被取消时抛出。</exception>
    internal async Task<T> InvokeAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken token)
    {
        // 不同键的嵌套命中可以正常返回；嵌套冷回源会等待自己持有的预算，因此直接拒绝。
        for (var frame = Current.Value; frame != null; frame = frame.Parent)
            if (frame.Owner == this)
                throw new InvalidOperationException("不允许在同一缓存实例的工厂中嵌套冷回源，请在工厂外组合依赖。");

        var tag = new KeyValuePair<string, object?>("cache.name", _name);
        var started = Stopwatch.GetTimestamp();
        IDisposable permit;
        var queued = false;
        try
        {
            var admission = CacheFactoryAdmission.EnterAsync(_resources, _queueTimeout, token);
            queued = !admission.IsCompleted;
            if (queued) Waiting.Add(1, tag);
            permit = await admission.ConfigureAwait(false);
        }
        catch (Exception error) when (error is InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            var reason = error is OperationCanceledException ? "cancelled" : error is TimeoutException ? "timeout" : "full";
            Rejected.Add(1, tag, new KeyValuePair<string, object?>("reason", reason));
            if (reason != "cancelled")
                _logger?.LogWarning(error, "混合缓存 {CacheName} 回源准入失败：{Reason}", _name, reason);
            throw;
        }
        finally
        {
            if (queued) Waiting.Add(-1, tag);
            WaitDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tag);
        }

        using (permit)
        using (var activity = Activities.StartActivity("cache.factory", ActivityKind.Internal))
        {
            activity?.SetTag("cache.name", _name);
            var previous = Current.Value;
            Current.Value = new Frame(this, key, previous);
            Active.Add(1, tag);
            try
            {
                token.ThrowIfCancellationRequested();
                return await factory(token).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                activity?.SetStatus(ActivityStatusCode.Error, error.GetType().Name);
                throw;
            }
            finally
            {
                Active.Add(-1, tag);
                Current.Value = previous;
            }
        }
    }

    /// <summary>
    /// 记录当前异步执行上下文中正在执行的回源调用。
    /// </summary>
    /// <param name="Owner">拥有该回源调用的调用器。</param>
    /// <param name="Key">规范化后的原生缓存键。</param>
    /// <param name="Parent">同一异步上下文中的上一级回源调用。</param>
    private sealed record Frame(FusionCacheFactoryInvoker Owner, string Key, Frame? Parent);
}
