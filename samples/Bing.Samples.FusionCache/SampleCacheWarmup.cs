using Bing.Caching;

namespace Bing.Samples.FusionCache;

/// <summary>
/// 对业务提供的有限热门键分批预热；不扫描缓存，不创建无界任务队列。
/// </summary>
public static class SampleCacheWarmup
{
    /// <summary>
    /// 等待当前批次结束后才开始下一批；单键失败计数后继续，总预算或调用方取消会停止新任务。
    /// </summary>
    /// <remarks>
    /// 总预算是协作取消，不会强行终止忽略令牌的工厂。成功表示缓存调用返回，可能是已有命中。
    /// 回源委托应自行创建独立作用域；本例宿主关闭旧值后备和后台完成，避免误报预热成功。
    /// </remarks>
    /// <param name="cache">待预热的命名缓存。</param>
    /// <param name="keys">有限键清单；执行期间不得修改。</param>
    /// <param name="load">接收逻辑键和取消令牌的业务回源委托。</param>
    /// <param name="batchSize">每批最多并发的缓存调用数。</param>
    /// <param name="budget">整个预热过程的取消预算。</param>
    /// <param name="cancellationToken">宿主启动或调用方取消令牌。</param>
    /// <returns>预热结果，取消不作为全部成功返回。</returns>
    public static async Task<SampleWarmupResult> RunAsync(IHybridCache cache, IReadOnlyList<string> keys,
        Func<string, CancellationToken, Task<string>> load, int batchSize, TimeSpan budget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(load);
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (budget <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(budget));
        using var timeout = new CancellationTokenSource(budget);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var token = linked.Token;
        var started = 0;
        var succeeded = 0;
        var failed = 0;
        var cancelled = 0;

        foreach (var batch in keys.Chunk(batchSize))
        {
            if (token.IsCancellationRequested) break;
            await Task.WhenAll(batch.Select(WarmOneAsync)).ConfigureAwait(false);
        }

        return new SampleWarmupResult(keys.Count, started, succeeded, failed, cancelled,
            timeout.IsCancellationRequested, cancellationToken.IsCancellationRequested);

        async Task WarmOneAsync(string key)
        {
            if (token.IsCancellationRequested) return;
            Interlocked.Increment(ref started);
            try
            {
                await cache.GetOrCreateAsync(key, ct => load(key, ct), cancellationToken: token).ConfigureAwait(false);
                Interlocked.Increment(ref succeeded);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Interlocked.Increment(ref cancelled);
            }
            catch (Exception)
            {
                // 示例聚合失败计数；生产接入可增加脱敏日志，避免逐键指标造成高基数。
                Interlocked.Increment(ref failed);
            }
        }
    }
}

/// <summary>
/// 一次预热的聚合结果；成功次数包括命中，不等于实际回源次数。
/// </summary>
/// <param name="Requested">输入键的数量。</param>
/// <param name="Started">已经发起的缓存调用数量。</param>
/// <param name="Succeeded">正常返回的调用数量。</param>
/// <param name="Failed">发生非本轮取消异常的调用数量。</param>
/// <param name="Cancelled">因本轮预算或外部令牌取消的调用数量。</param>
/// <param name="TimedOut">总预算令牌是否已取消。</param>
/// <param name="CallerCancelled">调用方令牌是否已取消。</param>
public sealed record SampleWarmupResult(int Requested, int Started, int Succeeded, int Failed,
    int Cancelled, bool TimedOut, bool CallerCancelled)
{
    /// <summary>
    /// 获取因停止调度而尚未开始的键数量。
    /// </summary>
    public int NotStarted => Requested - Started;
}
