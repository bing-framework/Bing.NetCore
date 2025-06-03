namespace Bing.Caching;

/// <summary>
/// 缓存键(<see cref="CacheKey"/>) 扩展
/// </summary>
public static class CacheKeyExtensions
{
    /// <summary>
    /// 验证缓存键
    /// </summary>
    /// <param name="cacheKey">缓存键</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Validate(this CacheKey cacheKey)
    {
        if (cacheKey == null)
            throw new ArgumentNullException(nameof(cacheKey));
        if (string.IsNullOrWhiteSpace(cacheKey.Key))
            throw new ArgumentNullException(nameof(cacheKey.Key));
    }
}
