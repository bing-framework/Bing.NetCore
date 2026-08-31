using Bing.Core.Data;

namespace Bing.Caching;

/// <summary>
/// 定义单一键值类型的本地缓存操作。
/// </summary>
/// <typeparam name="TKey">缓存键类型。</typeparam>
/// <typeparam name="TValue">缓存值类型。</typeparam>
public interface ISingleTypeCache<TKey, TValue> : IGetable<TKey, TValue>, IReader<IDictionary<TKey, TValue>>
{
    /// <summary>
    /// 获取当前缓存键数量。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 添加键和值。
    /// </summary>
    /// <param name="key">要添加的键。</param>
    /// <param name="value">要添加的值。</param>
    /// <returns>键不存在且已添加时返回 <c>true</c>；已存在时返回 <c>false</c>。</returns>
    bool Add(TKey key, TValue value);

    /// <summary>
    /// 更新已存在键的值。
    /// </summary>
    /// <param name="key">要更新的键。</param>
    /// <param name="value">要写入的值。</param>
    /// <returns>键存在且已更新时返回 <c>true</c>；不存在时返回 <c>false</c>。</returns>
    bool Update(TKey key, TValue value);

    /// <summary>
    /// 设置键和值。
    /// </summary>
    /// <param name="key">要设置的键。</param>
    /// <param name="value">要设置的值。</param>
    /// <returns>键存在时更新，否则添加；操作完成时返回 <c>true</c>。</returns>
    bool Set(TKey key, TValue value);

    /// <summary>
    /// 移除指定键。
    /// </summary>
    /// <param name="key">要移除的键。</param>
    /// <returns>键存在且已移除时返回 <c>true</c>；不存在时返回 <c>false</c>。</returns>
    bool Remove(TKey key);

    /// <summary>
    /// 移除多个键。
    /// </summary>
    /// <param name="keys">要移除的键数组。</param>
    /// <returns>实现定义的批量移除结果。</returns>
    bool Remove(TKey[] keys);

    /// <summary>
    /// 判断指定键是否存在。
    /// </summary>
    /// <param name="key">要检查的键。</param>
    /// <returns>键存在时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    bool Exists(TKey key);
}
