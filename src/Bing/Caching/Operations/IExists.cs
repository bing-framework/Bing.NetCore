namespace Bing.Caching.Operations
{
    /// <summary>
    /// 是否存在缓存
    /// </summary>
    public interface IExists
    {
        /// <summary>
        /// 是否存在指定缓存
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns></returns>
        bool Exists(string cacheKey);
    }
}
