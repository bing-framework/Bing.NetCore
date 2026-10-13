namespace Bing.Caching;

/// <summary>
/// 定义缓存异步访问契约。
/// </summary>
public partial interface ICache
{
    /// <summary>
    /// 异步检查指定缓存键是否存在。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>键存在时为 <see langword="true"/>，否则为 <see langword="false"/>。</returns>
    Task<bool> ExistsAsync(CacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步检查指定缓存键是否存在。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>键存在时为 <see langword="true"/>，否则为 <see langword="false"/>。</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取指定键的缓存值。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="type">用于反序列化缓存值的运行时类型。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存值；未命中时返回 <see langword="null"/>。</returns>
    Task<object> GetAsync(string key, Type type, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取指定键的缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存值；未命中时返回类型默认值。</returns>
    Task<T> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取指定键的缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存值；未命中时返回类型默认值。</returns>
    Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取指定键的缓存值。
    /// </summary>
    /// <remarks>缓存未命中时调用回源委托生成值并写入缓存；委托不接收取消令牌。</remarks>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="dataRetriever">缓存未命中时生成值的异步委托。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存值。</returns>
    Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取指定键的缓存值。
    /// </summary>
    /// <remarks>缓存未命中时调用回源委托生成值并写入缓存；委托不接收取消令牌。</remarks>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="dataRetriever">缓存未命中时生成值的异步委托。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存值。</returns>
    Task<T> GetAsync<T>(string key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取指定键的缓存值。
    /// </summary>
    /// <remarks>缓存未命中时调用回源委托生成值并写入缓存；委托接收取消令牌。</remarks>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="dataRetriever">用于生成缓存值的异步委托。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存值。</returns>
    Task<T> GetAsync<T>(CacheKey key, Func<CancellationToken, Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取指定键的缓存值。
    /// </summary>
    /// <remarks>缓存未命中时调用回源委托生成值并写入缓存；委托接收取消令牌。</remarks>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="dataRetriever">用于生成缓存值的异步委托。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存值。</returns>
    Task<T> GetAsync<T>(string key, Func<CancellationToken, Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步按输入顺序读取一组缓存键的值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的键集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按输入顺序对应的值；保留重复项，未命中位置为类型默认值。</returns>
    /// <remarks>输入集合只枚举一次；空集合不会访问缓存。</remarks>
    Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步按输入顺序读取一组缓存键的值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的键集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按输入顺序对应的值；保留重复项，未命中位置为类型默认值。</returns>
    /// <remarks>输入集合只枚举一次；空集合不会访问缓存。</remarks>
    Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按输入顺序异步流式读取一组缓存键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的键集合。输入只枚举一次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按输入顺序产生的缓存读取结果；重复键保留，未命中项的 <see cref="CacheEntry{T}.Hit"/> 为 <see langword="false"/>。</returns>
    IAsyncEnumerable<CacheEntry<T>> GetAllStreamAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按输入顺序异步流式读取一组缓存键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的键集合。输入只枚举一次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按输入顺序产生的缓存读取结果；重复键保留，未命中项的 <see cref="CacheEntry{T}.Hit"/> 为 <see langword="false"/>。</returns>
    IAsyncEnumerable<CacheEntry<T>> GetAllStreamAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取当前命名空间中逻辑键以前缀开头的缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前命名空间内匹配前缀的缓存值。</returns>
    Task<List<T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按逻辑键前缀异步流式读取缓存项。
    /// </summary>
    /// <typeparam name="T">缓存数据类型。</typeparam>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前命名空间中匹配前缀的缓存项。</returns>
    /// <remarks>
    /// Redis 扫描不是快照，可能重复产出键；并发修改可能改变结果。
    /// 已产出部分项后仍可能抛错。提前结束消费或取消不证明完成全节点扫描及末尾拓扑校验。
    /// </remarks>
    IAsyncEnumerable<CacheEntry<T>> GetByPrefixStreamAsync<T>(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步尝试写入缓存值，并仅在键不存在时生效。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="value">待写入的缓存值。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入成功时为 <see langword="true"/>；键已存在时为 <see langword="false"/>。</returns>
    Task<bool> TrySetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步尝试写入缓存值，并仅在键不存在时生效。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="value">待写入的缓存值。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入成功时为 <see langword="true"/>；键已存在时为 <see langword="false"/>。</returns>
    Task<bool> TrySetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步写入缓存值并覆盖同键的现有值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="value">待写入的缓存值。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步写入缓存值并覆盖同键的现有值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="value">待写入的缓存值。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步批量写入缓存值并覆盖同键的现有值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">待写入的键值集合。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SetAllAsync<T>(IDictionary<CacheKey, T> items, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步批量写入缓存值并覆盖同键的现有值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">待写入的键值集合。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SetAllAsync<T>(IDictionary<string, T> items, CacheOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除指定键的缓存值。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除指定键的缓存值。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步批量删除指定键的缓存值。
    /// </summary>
    /// <param name="keys">待删除的键集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RemoveAllAsync(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步批量删除指定键的缓存值。
    /// </summary>
    /// <param name="keys">待删除的键集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RemoveAllAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除当前命名空间中逻辑键以前缀开头的缓存项。
    /// </summary>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除当前命名空间中匹配通配模式的缓存项。
    /// </summary>
    /// <param name="pattern">逻辑键通配模式，例如 <c>test*</c>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步清理当前缓存命名空间。
    /// </summary>
    /// <remarks>清理通过扫描执行，不是原子操作；并发写入期间不保证最终为空。</remarks>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
