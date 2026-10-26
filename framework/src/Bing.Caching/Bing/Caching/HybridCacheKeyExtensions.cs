namespace Bing.Caching;

/// <summary>
/// 将 <see cref="CacheKey" /> 转换为混合缓存逻辑键的扩展方法集合。
/// </summary>
/// <remarks>
/// 统一复用 <see cref="CacheKey.Key" /> 的结果，避免调用方自行拼接前缀和格式化参数。
/// </remarks>
public static class HybridCacheKeyExtensions
{
    /// <summary>
    /// 读取指定逻辑键的未过期条目。
    /// </summary>
    /// <typeparam name="T">缓存值的引用或值类型。</typeparam>
    /// <param name="cache">目标混合缓存实例。</param>
    /// <param name="key">包含前缀和格式化参数的逻辑键。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <returns>包含命中状态和原始逻辑键的缓存条目；未命中时值类型为默认值。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cache" /> 或 <paramref name="key" /> 为 <see langword="null" /> 时抛出。</exception>
    public static Task<CacheEntry<T>> TryGetAsync<T>(this IHybridCache cache, CacheKey key,
        CancellationToken cancellationToken = default) =>
        cache.TryGetAsync<T>(GetKey(cache, key), cancellationToken);

    /// <summary>
    /// 读取指定逻辑键的条目，缺失时合并同键回源。
    /// </summary>
    /// <typeparam name="T">缓存值的引用或值类型。</typeparam>
    /// <param name="cache">目标混合缓存实例。</param>
    /// <param name="key">包含前缀和格式化参数的逻辑键。</param>
    /// <param name="factory">接收缓存取消令牌的异步回源委托。</param>
    /// <param name="options">基础 TTL；未指定时继承实例默认值。</param>
    /// <param name="tags">写入时关联的显式标签。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <returns>缓存命中值或回源工厂返回的值。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cache" />、<paramref name="key" /> 或 <paramref name="factory" /> 为 <see langword="null" /> 时抛出。</exception>
    public static Task<T> GetOrCreateAsync<T>(this IHybridCache cache, CacheKey key,
        Func<CancellationToken, Task<T>> factory, CacheOptions options = null,
        IEnumerable<string> tags = null, CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(GetKey(cache, key), factory, options, tags, cancellationToken);

    /// <summary>
    /// 写入指定逻辑键的条目。
    /// </summary>
    /// <typeparam name="T">缓存值的引用或值类型。</typeparam>
    /// <param name="cache">目标混合缓存实例。</param>
    /// <param name="key">包含前缀和格式化参数的逻辑键。</param>
    /// <param name="value">要写入的值，可为 <see langword="null" />。</param>
    /// <param name="options">基础 TTL；未指定时继承实例默认值。</param>
    /// <param name="tags">写入时关联的显式标签。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    public static Task SetAsync<T>(this IHybridCache cache, CacheKey key, T value,
        CacheOptions options = null, IEnumerable<string> tags = null, CancellationToken cancellationToken = default) =>
        cache.SetAsync(GetKey(cache, key), value, options, tags, cancellationToken);

    /// <summary>
    /// 移除指定逻辑键的条目及其旧值后备。
    /// </summary>
    /// <param name="cache">目标混合缓存实例。</param>
    /// <param name="key">包含前缀和格式化参数的逻辑键。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    public static Task RemoveAsync(this IHybridCache cache, CacheKey key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(GetKey(cache, key), cancellationToken);

    /// <summary>
    /// 软过期指定逻辑键的条目。
    /// </summary>
    /// <param name="cache">目标混合缓存实例。</param>
    /// <param name="key">包含前缀和格式化参数的逻辑键。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    public static Task ExpireAsync(this IHybridCache cache, CacheKey key, CancellationToken cancellationToken = default) =>
        cache.ExpireAsync(GetKey(cache, key), cancellationToken);

    /// <summary>
    /// 从缓存键对象提取实际逻辑键，并校验调用参数。
    /// </summary>
    /// <param name="cache">目标混合缓存实例。</param>
    /// <param name="key">包含前缀和格式化参数的逻辑键。</param>
    /// <returns>可直接交给混合缓存契约的逻辑键字符串。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cache" /> 或 <paramref name="key" /> 为 <see langword="null" /> 时抛出。</exception>
    private static string GetKey(IHybridCache cache, CacheKey key)
    {
        if (cache == null) throw new ArgumentNullException(nameof(cache));
        if (key == null) throw new ArgumentNullException(nameof(key));
        return key.Key;
    }
}
