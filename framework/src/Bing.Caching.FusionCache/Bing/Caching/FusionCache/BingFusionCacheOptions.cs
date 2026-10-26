namespace Bing.Caching.FusionCache;

/// <summary>
/// Bing 混合缓存的身份、输入与回源预算配置。
/// </summary>
/// <remarks>
/// 注册时复制配置，之后修改原实例不会影响已注册缓存；原生缓存策略通过 FusionCache builder 配置。
/// </remarks>
public sealed class BingFusionCacheOptions
{
    /// <summary>
    /// 获取或设置应用及环境命名空间，必须显式指定且不超过 256 个 UTF-16 字符。
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否将此实例注册为无名 <see cref="IHybridCache" />；同一容器最多允许一个。
    /// </summary>
    public bool UseAsDefault { get; set; }

    /// <summary>
    /// 获取或设置单个逻辑键允许的最大 UTF-16 长度，默认 1024。
    /// </summary>
    public int MaxKeyLength { get; set; } = 1024;

    /// <summary>
    /// 获取或设置单个标签允许的最大 UTF-16 长度，默认 256。
    /// </summary>
    public int MaxTagLength { get; set; } = 256;

    /// <summary>
    /// 获取或设置每次写入允许关联的标签数上限，默认 32。
    /// </summary>
    public int MaxTagsPerEntry { get; set; } = 32;

    /// <summary>
    /// 获取或设置可同时执行的回源工厂数量上限，默认 8，必须大于零。
    /// </summary>
    public int MaxConcurrentFactories { get; set; } = 8;

    /// <summary>
    /// 获取或设置等待回源许可的工厂数量上限，默认 128；零表示不排队。
    /// </summary>
    public int MaxQueuedFactories { get; set; } = 128;

    /// <summary>
    /// 获取或设置等待回源许可的最长时间，默认 30 秒；不包含工厂自身的执行时间。
    /// </summary>
    public TimeSpan FactoryQueueTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 校验当前配置并创建与原实例相互独立的快照。
    /// </summary>
    /// <returns>用于服务注册的配置副本。</returns>
    /// <exception cref="ArgumentException">命名空间为空或包含无效 UTF-16 字符时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">输入长度上限、回源预算或队列超时无效时抛出。</exception>
    internal BingFusionCacheOptions Snapshot()
    {
        Internal.FusionCacheKeyNormalizer.Validate(Namespace, 256, nameof(Namespace));
        if (MaxKeyLength <= 0 || MaxTagLength <= 0 || MaxTagsPerEntry <= 0 ||
            MaxConcurrentFactories <= 0 || MaxQueuedFactories < 0)
            throw new ArgumentOutOfRangeException(nameof(BingFusionCacheOptions), "缓存输入和回源预算无效。");
        if (FactoryQueueTimeout <= TimeSpan.Zero || FactoryQueueTimeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(FactoryQueueTimeout));
        return (BingFusionCacheOptions)MemberwiseClone();
    }
}
