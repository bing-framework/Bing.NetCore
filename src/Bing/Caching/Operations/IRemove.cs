using System.Collections.Generic;

namespace Bing.Caching.Operations
{
    /// <summary>
    /// 移除缓存
    /// </summary>
    public interface IRemove
    {
        /// <summary>
        /// 移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        void Remove(string cacheKey);

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        void RemoveByPrefix(string prefix);

        /// <summary>
        /// 批量移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKeys">缓存键集合</param>
        void RemoveAll(IEnumerable<string> cacheKeys);
    }
}
