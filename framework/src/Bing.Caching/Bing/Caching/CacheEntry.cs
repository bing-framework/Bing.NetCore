namespace Bing.Caching;

/// <summary>
/// 表示一次缓存批量读取的结果。
/// </summary>
/// <typeparam name="T">缓存值类型。</typeparam>
public readonly struct CacheEntry<T>
{
    /// <summary>
    /// 初始化一个 <see cref="CacheEntry{T}"/> 类型的实例。
    /// </summary>
    /// <param name="key">逻辑缓存键。</param>
    /// <param name="value">缓存值。未命中时为类型默认值。</param>
    /// <param name="hit">是否命中缓存。</param>
    public CacheEntry(string key, T value, bool hit)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value;
        Hit = hit;
    }

    /// <summary>
    /// 获取逻辑缓存键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 获取缓存值。
    /// </summary>
    /// <remarks>
    /// 未命中时为类型默认值。
    /// </remarks>
    public T Value { get; }

    /// <summary>
    /// 获取本次读取是否命中缓存。
    /// </summary>
    public bool Hit { get; }
}
