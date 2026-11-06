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
    /// 为 <c>null</c> 时不指定本次操作的有效期，由具体缓存实例决定默认过期策略，不等同于显式指定永久缓存。
    /// 默认值和无效有效期的校验时机可能因实现而异；迁移 Provider 时应显式核对默认配置。
    /// </remarks>
    public TimeSpan? Expiration { get; set; }
}
