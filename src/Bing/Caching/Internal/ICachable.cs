namespace Bing.Caching.Internal
{
    /// <summary>
    /// 可缓存
    /// </summary>
    public interface ICachable
    {
        /// <summary>
        /// 缓存键
        /// </summary>
        string CacheKey { get; }
    }
}
