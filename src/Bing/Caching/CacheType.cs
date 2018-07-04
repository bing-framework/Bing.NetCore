namespace Bing.Caching
{
    /// <summary>
    /// 缓存类型
    /// </summary>
    public enum CacheType
    {
        /// <summary>
        /// 本地
        /// </summary>
        Local = 1,
        /// <summary>
        /// Redis
        /// </summary>
        Redis = 2,
        /// <summary>
        /// 混合
        /// </summary>
        Hybrid = 3
    }
}
