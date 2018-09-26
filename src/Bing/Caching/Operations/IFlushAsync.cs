using System.Threading.Tasks;

namespace Bing.Caching.Operations
{
    /// <summary>
    /// 清空缓存
    /// </summary>
    public interface IFlushAsync
    {
        /// <summary>
        /// 清空所有缓存
        /// </summary>
        Task FlushAsync();
    }
}
