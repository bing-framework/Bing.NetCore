using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Internal;

/// <summary>
/// 将调用方 TTL 映射到 FusionCache 条目选项的复制型映射器。
/// </summary>
/// <remarks>
/// 每次调用都从启动配置复制，只覆盖 Bing 门面承诺的基础 TTL，避免调用方修改污染共享默认值。
/// </remarks>
/// <param name="defaults">官方缓存实例的默认条目选项。</param>
/// <param name="metadata">标签与清除操作使用的元数据选项，用于保留期校验。</param>
/// <param name="hasDistributedCache">当前实例是否启用了分布式缓存层。</param>
internal sealed class FusionCacheEntryOptionsMapper(FusionCacheEntryOptions defaults,
    FusionCacheEntryOptions? metadata = null, bool hasDistributedCache = false)
{
    /// <summary>
    /// 启动时复制的默认条目选项，后续调用均以其为模板。
    /// </summary>
    private readonly FusionCacheEntryOptions _defaults = defaults.Duplicate();

    /// <summary>
    /// 生成用于单次缓存操作的独立原生条目选项。
    /// </summary>
    /// <param name="options">调用方指定的基础 TTL；为 <see langword="null" /> 时使用实例默认值。</param>
    /// <returns>可直接交给官方缓存实例的条目选项副本。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options" /> 的有效期小于等于零或超出毫秒表示范围时抛出。</exception>
    /// <exception cref="InvalidOperationException">有效保留期超过元数据保留窗口时抛出。</exception>
    internal FusionCacheEntryOptions Map(CacheOptions? options = null)
    {
        var result = _defaults.Duplicate();
        if (options?.Expiration is not { } duration) return result;
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "缓存有效期必须大于零。");
        var milliseconds = duration.Ticks / TimeSpan.TicksPerMillisecond;
        if (duration.Ticks % TimeSpan.TicksPerMillisecond != 0) milliseconds++;
        if (milliseconds > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond)
            throw new ArgumentOutOfRangeException(nameof(options), "缓存有效期超出毫秒表示范围。");
        result.Duration = TimeSpan.FromTicks(milliseconds * TimeSpan.TicksPerMillisecond);
        if (metadata != null) FusionCacheRetentionValidator.Validate(result, metadata, hasDistributedCache);
        return result;
    }
}
