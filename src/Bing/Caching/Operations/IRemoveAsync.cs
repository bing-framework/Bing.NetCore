using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bing.Caching.Operations
{
    /// <summary>
    /// 移除缓存
    /// </summary>
    public interface IRemoveAsync
    {
        /// <summary>
        /// 移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        Task RemoveAsync(string cacheKey);

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        Task RemoveByPrefixAsync(string prefix);

        /// <summary>
        /// 批量移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKeys">缓存键集合</param>
        Task RemoveAllAsync(IEnumerable<string> cacheKeys);
    }
}
