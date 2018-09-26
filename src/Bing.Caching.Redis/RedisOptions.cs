using Bing.Caching.Options;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// Redis 选项
    /// </summary>
    public class RedisOptions:CacheProviderOptionsBase
    {
        /// <summary>
        /// 数据库配置
        /// </summary>
        public RedisDbOptions DbConfig { get; set; } = new RedisDbOptions();

        /// <summary>
        /// 初始化一个<see cref="RedisOptions"/>类型的实例
        /// </summary>
        public RedisOptions()
        {
            this.CacheProviderType = CacheProviderType.Redis;
        }
    }
}
