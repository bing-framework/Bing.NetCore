namespace Bing.Caching
{
    /// <summary>
    /// 缓存级别
    /// </summary>
    public enum CacheLevel
    {
        /// <summary>
        /// 本地内存缓存
        /// </summary>
        Local = 1,
        /// <summary>
        /// 磁盘文件
        /// </summary>
        Disk = 2,
        /// <summary>
        /// 分布式缓存
        /// </summary>
        Distributed = 3
    }
}
