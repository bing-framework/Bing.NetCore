using Bing.Caching.FusionCache.Internal;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache;

/// <summary>
/// 将 <see cref="IHybridCache" /> 契约转发到官方 FusionCache 实例的命名门面。
/// </summary>
/// <remarks>
/// 不拥有或重复释放依赖注入管理的底层缓存资源；创建时校验 Backplane 订阅状态与保留期契约。
/// </remarks>
internal sealed class FusionCacheManager : IHybridCache
{
    /// <summary>
    /// 当前名称对应的官方缓存实例。
    /// </summary>
    private readonly IFusionCache _cache;

    /// <summary>
    /// 负责逻辑键、标签编码和输入限制的规范化器。
    /// </summary>
    private readonly FusionCacheKeyNormalizer _keys;

    /// <summary>
    /// 每次调用生成原生条目选项的映射器。
    /// </summary>
    private readonly FusionCacheEntryOptionsMapper _options;

    /// <summary>
    /// 只读查询专用的私有快照。FusionCache 2.9 的 TryGet 路径只读取这些选项，
    /// 不向回源工厂或 Backplane 暴露；禁止与写入、回源操作共享，避免热路径重复复制。
    /// </summary>
    private readonly FusionCacheEntryOptions _readOptions;

    /// <summary>
    /// 统一管理回源准入、递归检测和诊断的调用器。
    /// </summary>
    private readonly FusionCacheFactoryInvoker _factory;

    /// <summary>
    /// 用于标签失效与清除操作的元数据条目选项快照。
    /// </summary>
    private readonly FusionCacheEntryOptions _tagOptions;

    /// <summary>
    /// 初始化一个 <see cref="FusionCacheManager" /> 类的新实例。
    /// </summary>
    /// <param name="cache">已配置的官方缓存实例。</param>
    /// <param name="options">注册时冻结的 Bing 缓存配置。</param>
    /// <param name="tagOptions">标签与清除操作使用的元数据选项。</param>
    /// <param name="logger">记录回源准入失败的可选日志记录器。</param>
    /// <exception cref="InvalidOperationException">首次 Backplane 订阅失败时抛出。</exception>
    internal FusionCacheManager(IFusionCache cache, BingFusionCacheOptions options, FusionCacheEntryOptions tagOptions, ILogger? logger)
    {
        _cache = cache;
        if (cache.Backplane is FusionCacheStartupBackplane { IsSubscribed: false } backplane)
            throw new InvalidOperationException("混合缓存首次 Backplane 订阅失败，实例未就绪。", backplane.SubscriptionError);
        _keys = new FusionCacheKeyNormalizer(options);
        _tagOptions = tagOptions.Duplicate();
        FusionCacheRetentionValidator.Validate(cache.DefaultEntryOptions, _tagOptions, cache.HasDistributedCache);
        _options = new FusionCacheEntryOptionsMapper(cache.DefaultEntryOptions, _tagOptions, cache.HasDistributedCache);
        _readOptions = _options.Map();
        _readOptions.AllowStaleOnReadOnly = false;
        _factory = new FusionCacheFactoryInvoker(cache.CacheName, options, logger);
    }

    /// <inheritdoc />
    public string Name => _cache.CacheName;

    /// <inheritdoc />
    public async Task<CacheEntry<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var nativeKey = _keys.Key(key);
        var result = await _cache.TryGetAsync<T>(nativeKey, _readOptions, cancellationToken).ConfigureAwait(false);
        return new CacheEntry<T>(key, result.HasValue ? result.Value : default!, result.HasValue);
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        CacheOptions? options = null, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var nativeKey = _keys.Key(key);
        var nativeTags = _keys.Tags(tags);
        var entryOptions = _options.Map(options);
        _factory.CheckRecursion(nativeKey);
        return await _cache.GetOrSetAsync<T>(nativeKey,
            (_, token) => _factory.InvokeAsync(nativeKey, factory, token),
            options: entryOptions, tags: nativeTags, token: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, CacheOptions? options = null,
        IEnumerable<string>? tags = null, CancellationToken cancellationToken = default) =>
        _cache.SetAsync(_keys.Key(key), value, _options.Map(options), _keys.Tags(tags), cancellationToken).AsTask();

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(_keys.Key(key), _options.Map(), cancellationToken).AsTask();

    /// <inheritdoc />
    public Task ExpireAsync(string key, CancellationToken cancellationToken = default) =>
        _cache.ExpireAsync(_keys.Key(key), _options.Map(), cancellationToken).AsTask();

    /// <inheritdoc />
    public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default) =>
        _cache.RemoveByTagAsync(_keys.Tag(tag), _tagOptions.Duplicate(), cancellationToken).AsTask();

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _cache.ClearAsync(allowFailSafe: false, options: _tagOptions.Duplicate(), token: cancellationToken).AsTask();
    }
}
