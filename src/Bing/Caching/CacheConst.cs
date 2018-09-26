namespace Bing.Caching
{
    /// <summary>
    /// 缓存常量
    /// </summary>
    public class CacheConst
    {
        /// <summary>
        /// 内存流 片段
        /// </summary>
        public const string InMemorySection = "bingcache:inmemory";

        /// <summary>
        /// Redis 片段
        /// </summary>
        public const string RedisSection = "bingcache:redis";

        /// <summary>
        /// Memcached 片段
        /// </summary>
        public const string MemcachedSection = "bingcache:memcached";

        /// <summary>
        /// SQLite 片段
        /// </summary>
        public const string SQLiteSection = "bingcache:sqlite";

    }
}
