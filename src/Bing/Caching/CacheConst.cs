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

        /// <summary>
        /// 默认 内存缓存 名称
        /// </summary>
        public const string DefaultInMemoryName = "DefaultInMemory";

        /// <summary>
        /// 默认 Redis 名称
        /// </summary>
        public const string DefaultRedisName = "DefaultRedis";

        /// <summary>
        /// 默认 Memcached 名称
        /// </summary>
        public const string DefaultMemcachedName = "DefaultMemcached";
    }
}
