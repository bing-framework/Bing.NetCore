using System.Threading.Tasks;

namespace Bing.Caching.Operations
{
    /// <summary>
    /// 是否存在缓存
    /// </summary>
    public interface IExistsAsync
    {
        /// <summary>
        /// 是否存在指定缓存
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns></returns>
        Task<bool> ExistsAsync(string cacheKey);
    }
}
