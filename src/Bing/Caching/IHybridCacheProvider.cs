namespace Bing.Caching
{
    /// <summary>
    /// 混合缓存提供程序
    /// </summary>
    public interface IHybridCacheProvider:ICacheManager
    {
        /// <summary>
        /// 缓存级别
        /// </summary>
        CacheLevel CacheLevel { get; }
    }
}
