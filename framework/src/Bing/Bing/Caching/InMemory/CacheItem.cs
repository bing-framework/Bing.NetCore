namespace Bing.Caching.InMemory;

/// <summary>
/// 缓存项
/// </summary>
internal class CacheItem
{
    /// <summary>
    /// 值
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiredTime { get; set; }

    /// <summary>
    /// 是否已过期
    /// </summary>
    public bool Expired => ExpiredTime <= DateTime.Now;

    /// <summary>
    /// 访问时间
    /// </summary>
    public DateTime VisitTime{ get; private set; }

    /// <summary>
    /// 初始化一个<see cref="CacheItem"/>类型的实例
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="expired">过期时间</param>
    public CacheItem(object value, TimeSpan? expired = null) => Set(value, expired);

    /// <summary>
    /// 设置值及过期时间
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="expired">过期时间</param>
    public void Set(object value, TimeSpan? expired = null)
    {
        Value = value;
        var now = VisitTime = DateTime.Now;
        ExpiredTime = expired == null ? DateTime.MaxValue : now.AddSeconds(expired.Value.TotalSeconds);
    }

    /// <summary>
    /// 更新访问时间并返回数值
    /// </summary>
    public object Visit()
    {
        VisitTime=DateTime.Now;
        return Value;
    }
}
