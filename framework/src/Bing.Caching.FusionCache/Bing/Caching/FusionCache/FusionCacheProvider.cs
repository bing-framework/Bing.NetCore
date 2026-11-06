namespace Bing.Caching.FusionCache;

/// <summary>
/// 按名称提供启动时解析的混合缓存门面。
/// </summary>
/// <remarks>
/// 仅缓存已构造的门面引用，不拥有或释放底层 FusionCache 资源。
/// </remarks>
internal sealed class FusionCacheProvider : IHybridCacheProvider
{
    /// <summary>
    /// 以不区分区域性的序号比较缓存的注册名称。
    /// </summary>
    private readonly IReadOnlyDictionary<string, IHybridCache> _caches;

    /// <summary>
    /// 初始化一个 <see cref="FusionCacheProvider" /> 类型的实例。
    /// </summary>
    /// <param name="caches">已构造的命名缓存门面集合。</param>
    ///
    /// <exception cref="ArgumentException">存在重复的缓存名称时抛出。</exception>
    /// <remarks>
    /// 初始化一个 <see cref="FusionCacheProvider" /> 类的新实例。
    /// </remarks>
    public FusionCacheProvider(IEnumerable<FusionCacheManager> caches) =>
        _caches = caches.ToDictionary(x => x.Name, x => (IHybridCache)x, StringComparer.Ordinal);

    /// <inheritdoc />
    public IHybridCache GetCache(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _caches.TryGetValue(name, out var cache)
            ? cache
            : throw new InvalidOperationException($"未注册混合缓存实例：{name}。");
    }
}
