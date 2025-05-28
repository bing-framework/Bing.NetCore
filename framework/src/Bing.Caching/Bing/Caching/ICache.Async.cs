namespace Bing.Caching;

// 缓存 - 异步
public partial interface ICache
{
    /// <summary>
    /// 检查指定的缓存键是否存在于缓存中。
    /// </summary>
    /// <param name="key">缓存键，为<see cref="CacheKey"/>类型，提供了一种类型安全的方式来指定缓存键。</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果缓存键存在，则返回true；否则，返回false。</returns>
    Task<bool> ExistsAsync(CacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查指定的缓存键是否存在于缓存中。
    /// </summary>
    /// <param name="key">缓存键，为字符串类型，直接指定了缓存键的名称。</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果缓存键存在，则返回true；否则，返回false。</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中获取指定键的值。
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="type">要返回的对象的类型。</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与指定键关联的值。</returns>
    Task<object> GetAsync(string key, Type type, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中获取指定键的值。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与指定键关联的值。</returns>
    Task<T> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中获取指定键的值。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与指定键关联的值。</returns>
    Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中获取指定键的值，如果键不存在，则使用提供的函数获取值，然后将其添加到缓存中。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="dataRetriever">用于获取值的函数。</param>
    /// <param name="options">缓存选项，包括过期时间等。</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与指定键关联的值。</returns>
    Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中获取指定键的值，如果键不存在，则使用提供的函数获取值，然后将其添加到缓存中。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="dataRetriever">用于获取值的函数。</param>
    /// <param name="options">缓存选项，包括过期时间等。</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与指定键关联的值。</returns>
    Task<T> GetAsync<T>(string key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中获取一组键对应的值。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="keys">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与指定键集合关联的值的列表。</returns>
    Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中获取一组键对应的值。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="keys">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与指定键集合关联的值的列表。</returns>
    Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据前缀获取匹配的元素列表。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="prefix">缓存键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含所有前缀匹配给定字符串的元素的列表。</returns>
    Task<List<T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试将值设置到缓存中，当缓存已存在则忽略，设置成功返回true。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="options">缓存选项，包括过期时间等。</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果成功设置则返回true，否则返回false。</returns>
    Task<bool> TrySetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试将值设置到缓存中，当缓存已存在则忽略，设置成功返回true。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="options">缓存选项，包括过期时间等。</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果成功设置则返回true，否则返回false。</returns>
    Task<bool> TrySetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将值设置到缓存中，当缓存已存在则覆盖。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="options">缓存选项，包括过期时间等。</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将值设置到缓存中，当缓存已存在则覆盖。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="options">缓存选项，包括过期时间等。</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将多个键值对设置到缓存中。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="items">缓存项集合</param>
    /// <param name="options">缓存选项，包括过期时间等。</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAllAsync<T>(IDictionary<CacheKey, T> items, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将多个键值对设置到缓存中。
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="items">缓存项集合</param>
    /// <param name="options">缓存选项，包括过期时间等。</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAllAsync<T>(IDictionary<string, T> items, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中移除指定的键。
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中移除指定的键。
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中移除多个指定的键。
    /// </summary>
    /// <param name="keys">缓存键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAllAsync(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从缓存中移除多个指定的键。
    /// </summary>
    /// <param name="keys">缓存键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAllAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据前缀从缓存中移除键。
    /// </summary>
    /// <param name="prefix">缓存键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据匹配模式从缓存中移除键。
    /// </summary>
    /// <param name="pattern">缓存键模式,范例: test*</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除缓存中的所有键和值。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
