using Bing.Caching.Options;
using Enyim.Caching.Configuration;

namespace Bing.Caching.Memcached
{
    /// <summary>
    /// Memcached 选项
    /// </summary>
    public class MemcachedOptions:CacheProviderOptionsBase
    {
        /// <summary>
        /// 数据库配置
        /// </summary>
        public MemcachedClientOptions DbConfig { get; set; } = new MemcachedClientOptions();

        /// <summary>
        /// 初始化一个<see cref="MemcachedOptions"/>类型的实例
        /// </summary>
        public MemcachedOptions()
        {
            this.CacheProviderType = CacheProviderType.Memcached;
        }
    }
}
