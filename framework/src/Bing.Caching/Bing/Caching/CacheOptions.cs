namespace Bing.Caching;

/// <summary>
/// 配置单次缓存读取或写入操作的行为。
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// 获取或设置缓存项的过期时间间隔。
    /// </summary>
    /// <remarks>
    /// 为 <c>null</c> 时不指定本次操作的绝对过期时间，由具体缓存实现决定默认过期策略；并非所有实现都会使用相同的默认值。
    /// </remarks>
    public TimeSpan? Expiration { get; set; }
}
