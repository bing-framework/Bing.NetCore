using Bing.Caching.Options;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// Redis 数据库选项
    /// </summary>
    public class RedisDbOptions:RedisOptionsBase
    {
        /// <summary>
        /// Redis 数据库索引
        /// </summary>
        public int Database { get; set; } = 0;
    }
}
