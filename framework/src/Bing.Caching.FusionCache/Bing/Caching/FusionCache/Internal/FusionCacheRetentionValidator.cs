using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Internal;

/// <summary>
/// 校验数据条目的物理保留窗口不与标签及清除标记冲突。
/// </summary>
/// <remarks>
/// 失效标记不得先于其保护的数据消失，因此按固定版本的 L1/L2、Fail-Safe 与抖动规则计算最短物理保留窗口。
/// </remarks>
internal static class FusionCacheRetentionValidator
{
    /// <summary>
    /// 校验数据条目的物理保留窗口不超过标签与清除元数据的最短保留窗口。
    /// </summary>
    /// <param name="data">业务数据条目的原生选项。</param>
    /// <param name="metadata">标签与清除元数据条目的原生选项。</param>
    /// <param name="hasDistributedCache">当前实例是否启用了分布式缓存层。</param>
    /// <exception cref="InvalidOperationException">元数据跳过缓存层读写、保留期为负或数据窗口超出标记窗口时抛出。</exception>
    internal static void Validate(FusionCacheEntryOptions data, FusionCacheEntryOptions metadata, bool hasDistributedCache)
    {
        var dataWindow = PhysicalDuration(data, distributed: false, includeJitter: true);
        var markerWindow = PhysicalDuration(metadata, distributed: false, includeJitter: false);
        if (hasDistributedCache)
        {
            dataWindow = Max(dataWindow, PhysicalDuration(data, distributed: true, includeJitter: false));
            var distributedMarker = PhysicalDuration(metadata, distributed: true, includeJitter: false);
            if (distributedMarker < markerWindow) markerWindow = distributedMarker;
        }
        if (metadata.SkipMemoryCacheRead || metadata.SkipMemoryCacheWrite ||
            (hasDistributedCache && (metadata.SkipDistributedCacheRead || metadata.SkipDistributedCacheWrite)))
            throw new InvalidOperationException("标签/Clear 元数据不能跳过已启用缓存层的读写。");
        if (markerWindow <= TimeSpan.Zero || dataWindow > markerWindow)
            throw new InvalidOperationException("数据的 L1/L2、Fail-Safe 和抖动保留窗口不能超过标签/Clear 元数据的最短物理保留窗口。请延长 TagsDefaultEntryOptions 或缩短业务保留期。");
    }

    /// <summary>
    /// 计算指定条目选项在目标缓存层上的物理保留时长。
    /// </summary>
    /// <param name="options">待计算的条目选项。</param>
    /// <param name="distributed">是否计算分布式缓存层；为 <see langword="false" /> 时计算内存缓存层。</param>
    /// <param name="includeJitter">是否将随机抖动计入物理过期时间。</param>
    /// <returns>目标缓存层的最长物理保留时长。</returns>
    /// <exception cref="InvalidOperationException">保留期或抖动为负，或二者之和超出允许范围时抛出。</exception>
    private static TimeSpan PhysicalDuration(FusionCacheEntryOptions options, bool distributed, bool includeJitter)
    {
        var duration = (distributed ? options.DistributedCacheDuration : options.MemoryCacheDuration) ?? options.Duration;
        if (duration < TimeSpan.Zero || options.JitterMaxDuration < TimeSpan.Zero)
            throw new InvalidOperationException("缓存保留期和抖动不能为负数。");
        if (options.IsFailSafeEnabled)
            duration = Max(duration, distributed ? options.DistributedCacheFailSafeMaxDuration ?? options.FailSafeMaxDuration : options.FailSafeMaxDuration);
        // L1 的物理过期包含抖动；标记只取最短值，不能依赖随机抖动保护数据。
        if (includeJitter && options.JitterMaxDuration > TimeSpan.Zero)
        {
            if (duration > TimeSpan.MaxValue - options.JitterMaxDuration)
                throw new InvalidOperationException("缓存保留期与抖动之和超出允许范围。");
            duration += options.JitterMaxDuration;
        }
        return duration;
    }

    /// <summary>
    /// 返回两个时长中较大者。
    /// </summary>
    /// <param name="left">左侧时长。</param>
    /// <param name="right">右侧时长。</param>
    /// <returns>较长的时长。</returns>
    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;
}
