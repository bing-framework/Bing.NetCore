using Bing.Caching.CacheStats;
using Bing.Caching.Operations;

namespace Bing.Caching
{
    /// <summary>
    /// 缓存提供程序
    /// </summary>
    public interface ICacheProvider : ISet, ISetAsync,
        IGet, IGetAsync,
        IRemove, IRemoveAsync,
        IExists, IExistsAsync,
        IRefresh, IRefreshAsync,
        ICount,
        IFlush, IFlushAsync
    {
        /// <summary>
        /// 是否分布式缓存
        /// </summary>
        bool IsDistributedCache { get; }

        /// <summary>
        /// 顺序
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 最大随机秒数
        /// </summary>
        int MaxRdSecond { get; }

        /// <summary>
        /// 缓存提供程序类型
        /// </summary>
        CacheProviderType CacheProviderType { get; }

        /// <summary>
        /// 缓存统计信息
        /// </summary>
        CacheStatsInfo CacheStatsInfo { get; }
    }
}
