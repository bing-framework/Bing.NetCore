namespace Bing.Caching.InMemory;

/// <summary>
/// 表示具有绝对过期时间的内存缓存项。
/// </summary>
internal class CacheItem
{
    /// <summary>
    /// 获取或设置缓存的原始对象引用。
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// 获取或设置缓存项的绝对过期时间。
    /// </summary>
    public DateTime ExpiredTime { get; set; }

    /// <summary>
    /// 获取缓存项是否已按本地时间过期。
    /// </summary>
    public bool Expired => ExpiredTime <= DateTime.Now;

    /// <summary>
    /// 获取最近一次设置或访问的本地时间。
    /// </summary>
    public DateTime VisitTime{ get; private set; }

    /// <summary>
    /// 使用缓存值和可选绝对过期时长初始化 <see cref="CacheItem"/> 的实例。
    /// </summary>
    /// <param name="value">要缓存的原始对象引用。</param>
    /// <param name="expired">从设置时刻起计算的可选过期时长；为空时永不过期。</param>
    public CacheItem(object value, TimeSpan? expired = null) => Set(value, expired);

    /// <summary>
    /// 设置缓存值并重置绝对过期时间。
    /// </summary>
    /// <param name="value">要缓存的原始对象引用。</param>
    /// <param name="expired">从当前时刻起计算的可选过期时长；为空时设置为 <see cref="DateTime.MaxValue"/>。</param>
    public void Set(object value, TimeSpan? expired = null)
    {
        Value = value;
        var now = VisitTime = DateTime.Now;
        ExpiredTime = expired == null ? DateTime.MaxValue : now.AddSeconds(expired.Value.TotalSeconds);
    }

    /// <summary>
    /// 更新访问时间并返回缓存值。
    /// </summary>
    /// <returns>当前缓存的原始对象引用。</returns>
    /// <remarks>访问不会延长 <see cref="ExpiredTime"/>，因此不是滑动过期。</remarks>
    public object Visit()
    {
        VisitTime=DateTime.Now;
        return Value;
    }
}
