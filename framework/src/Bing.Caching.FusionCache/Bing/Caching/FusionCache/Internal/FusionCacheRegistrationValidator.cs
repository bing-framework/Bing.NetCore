using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Internal;

/// <summary>
/// 校验最终原生配置中由 Bing 门面负责的固定契约。
/// </summary>
/// <remarks>
/// 在官方 builder 执行全部用户配置回调后运行，防止缓存身份、标签语义、Backplane 就绪条件或显式 L2 配置被悄然降级。
/// </remarks>
internal static class FusionCacheRegistrationValidator
{
    /// <summary>
    /// 校验缓存身份前缀、标签行为、自动恢复和 Backplane 必达契约。
    /// </summary>
    /// <param name="builder">已完成用户配置的官方 FusionCache builder。</param>
    /// <param name="options">builder 解析出的最终原生配置。</param>
    /// <param name="name">注册时指定的缓存实例名称。</param>
    /// <param name="prefix">Bing 生成的原生键前缀。</param>
    /// <exception cref="InvalidOperationException">任一固定契约被覆盖或缺失时抛出。</exception>
    internal static void Validate(IFusionCacheBuilder builder, FusionCacheOptions options, string name, string prefix)
    {
        if (options.CacheName != name || options.CacheKeyPrefix != prefix ||
            options.BackplaneChannelPrefix != prefix + "bus" ||
            (builder.UseCacheKeyPrefix && (builder.CacheKeyPrefix != prefix || builder.AddCacheKeyPrefixSeparator)))
            throw new InvalidOperationException("不得覆盖 Bing FusionCache 的 CacheName、CacheKeyPrefix 或 BackplaneChannelPrefix。");
        if (options.DisableTagging || options.RemoveByTagBehavior != RemoveByTagBehavior.Remove)
            throw new InvalidOperationException("Bing 混合缓存必须启用标签，并使用 Remove 标签失效语义。");
        if (options.DefaultEntryOptionsProvider != null)
            throw new InvalidOperationException("Bing 混合缓存不支持 DefaultEntryOptionsProvider；请使用实例默认条目选项或单次 CacheOptions 设置有效期。");
        if (options.EnableAutoRecovery && !(options.AutoRecoveryMaxItems > 0))
            throw new InvalidOperationException("启用自动恢复时必须配置有限且为正的 AutoRecoveryMaxItems。");
        if (!options.WaitForInitialBackplaneSubscribe)
            throw new InvalidOperationException("Bing 混合缓存必须等待首次 Backplane 订阅，不能在后台初始化后立即报告就绪。");
        // 显式启用 L2 的部署不允许悄悄降级为纯 L1。
        if (builder.UseRegisteredDistributedCache || builder.DistributedCache != null || builder.DistributedCacheFactory != null)
        {
            builder.ThrowIfMissingDistributedCache = true;
            builder.ThrowIfMissingSerializer = true;
        }
        if (builder.UseRegisteredBackplane || builder.Backplane != null || builder.BackplaneFactory != null)
            builder.ThrowIfMissingBackplane = true;
    }

    /// <summary>
    /// 校验每次写入使用的默认条目有效期必须大于零。
    /// </summary>
    /// <param name="options">即将生效的默认条目选项。</param>
    /// <exception cref="InvalidOperationException"><paramref name="options" /> 的有效期小于等于零时抛出。</exception>
    internal static void ValidateEntryOptions(FusionCacheEntryOptions options)
    {
        if (options.Duration <= TimeSpan.Zero)
            throw new InvalidOperationException("默认缓存有效期必须大于零。");
    }
}
