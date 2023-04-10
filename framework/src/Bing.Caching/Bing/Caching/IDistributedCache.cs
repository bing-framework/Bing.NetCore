namespace Bing.Caching;

/// <summary>
/// 分布式缓存
/// </summary>
/// <typeparam name="TCacheItem">缓存项类型</typeparam>
/// <typeparam name="TCacheKey">缓存键类型</typeparam>
public interface IDistributedCache<TCacheItem, TCacheKey>
    where TCacheItem : class
{
    /// <summary>
    /// 从缓存中获取数据
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="hideErrors">是否隐藏错误</param>
    TCacheItem Get(TCacheKey key, bool? hideErrors = null);

    /// <summary>
    /// 从缓存中获取多个数据
    /// </summary>
    /// <param name="keys">缓存键集合</param>
    /// <param name="hideErrors">是否隐藏错误</param>
    KeyValuePair<TCacheKey, TCacheItem> GetMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null);
}
