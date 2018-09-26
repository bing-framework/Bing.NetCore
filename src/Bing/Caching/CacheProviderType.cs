namespace Bing.Caching
{
    /// <summary>
    /// 缓存提供程序类型
    /// </summary>
    public enum CacheProviderType
    {
        /// <summary>
        /// 内存缓存
        /// </summary>
        InMemory,
        /// <summary>
        /// Memcached
        /// </summary>
        Memcached,
        /// <summary>
        /// Redis
        /// </summary>
        Redis,
        /// <summary>
        /// SQLite
        /// </summary>
        // ReSharper disable once InconsistentNaming
        SQLite,
    }
}
