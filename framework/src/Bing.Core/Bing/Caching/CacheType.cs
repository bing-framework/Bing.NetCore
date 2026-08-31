namespace Bing.Caching;

/// <summary>
/// 定义内置缓存实现的稳定类型标识符。
/// </summary>
public class CacheType
{
    /// <summary>
    /// 内存缓存实现标识符，值为 <c>Memory</c>。
    /// </summary>
    public const string Memory = nameof(Memory);

    /// <summary>
    /// Redis 缓存实现标识符，值为 <c>Redis</c>。
    /// </summary>
    public const string Redis = nameof(Redis);
}
