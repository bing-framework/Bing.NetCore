namespace Bing.Caching;

/// <summary>
/// 提供具备同键回源合并能力的异步单键混合缓存契约。
/// </summary>
/// <remarks>
/// 实例名称决定缓存策略和隔离空间；同一实例中的逻辑键应固定数据类型。
/// 此接口不提供原子条件写入、扫描或事务一致性保证；开启后台完成或提前刷新时，工厂必须自行创建并释放依赖作用域。
/// </remarks>
public interface IHybridCache
{
    /// <summary>
    /// 获取注册时指定的缓存实例名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 读取指定逻辑键的未过期条目。
    /// </summary>
    /// <typeparam name="T">缓存值的引用或值类型。</typeparam>
    /// <param name="key">实例内的逻辑键，不能为空。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <returns>包含命中状态和原始逻辑键的缓存条目；未命中时值类型为默认值。</returns>
    /// <remarks>
    /// 通过 <see cref="CacheEntry{T}.Hit" /> 区分 null 命中与缺失，<see cref="CacheEntry{T}.Key" /> 保留传入的逻辑键。
    /// </remarks>
    Task<CacheEntry<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取指定逻辑键的条目，缺失时合并同键回源。
    /// </summary>
    /// <typeparam name="T">缓存值的引用或值类型。</typeparam>
    /// <param name="key">实例内的逻辑键，不能为空。</param>
    /// <param name="factory">接收缓存提供的取消令牌的异步回源委托。</param>
    /// <param name="options">基础 TTL；未指定时继承实例默认值。</param>
    /// <param name="tags">写入时关联的显式标签。</param>
    /// <param name="cancellationToken">调用方取消令牌，与后台工厂令牌不一定相同。</param>
    /// <returns>缓存命中值或回源工厂返回的值。</returns>
    /// <remarks>
    /// 工厂异常不会被转换成缓存的 null；同一实例内不允许同键递归回源。
    /// </remarks>
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        CacheOptions options = null, IEnumerable<string> tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 写入指定逻辑键的条目，可保存 null。
    /// </summary>
    /// <typeparam name="T">缓存值的引用或值类型。</typeparam>
    /// <param name="key">实例内的逻辑键，不能为空。</param>
    /// <param name="value">要写入的值，可为 null。</param>
    /// <param name="options">基础 TTL；未指定时继承实例默认值。</param>
    /// <param name="tags">写入时关联的显式标签。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <remarks>
    /// 写入不保证与并发回源构成线性一致性屏障。
    /// </remarks>
    Task SetAsync<T>(string key, T value, CacheOptions options = null,
        IEnumerable<string> tags = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除指定逻辑键的条目及其旧值后备。
    /// </summary>
    /// <param name="key">实例内的逻辑键，不能为空。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 软过期指定逻辑键的条目。
    /// </summary>
    /// <param name="key">实例内的逻辑键，不能为空。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <remarks>
    /// 启用 Fail-Safe 时，旧值仍可能作为回源失败的后备。
    /// </remarks>
    Task ExpireAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使指定标签关联的条目失效。
    /// </summary>
    /// <param name="tag">写入条目时关联的标签，不能为空。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <remarks>
    /// 失效后的条目不能再作为 Fail-Safe 旧值后备。
    /// </remarks>
    Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除当前实例的可见缓存并禁止旧值后备。
    /// </summary>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <remarks>
    /// 不承诺立即物理擦除分布式缓存中的全部条目。
    /// </remarks>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
