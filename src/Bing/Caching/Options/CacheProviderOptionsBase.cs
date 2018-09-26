namespace Bing.Caching.Options
{
    /// <summary>
    /// 缓存提供程序选项基类
    /// </summary>
    public class CacheProviderOptionsBase
    {
        /// <summary>
        /// 缓存提供程序类型
        /// </summary>
        public CacheProviderType CacheProviderType { get; set; }

        /// <summary>
        /// 最大随机秒数
        /// </summary>
        public int MaxRdSecond { get; set; } = 120;

        /// <summary>
        /// 顺序
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// 是否启用日志记录
        /// </summary>
        public bool EnableLogging { get; set; }
    }
}
