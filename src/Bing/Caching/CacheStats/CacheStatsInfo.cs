using System.Collections.Concurrent;

namespace Bing.Caching.CacheStats
{
    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatsInfo
    {
        /// <summary>
        /// 计数器字典
        /// </summary>
        private readonly ConcurrentDictionary<string, CacheStatsCounter> _counters;

        /// <summary>
        /// 默认缓存键
        /// </summary>
        private const string DefaultKey = "bing_cache_stats";

        /// <summary>
        /// 初始化一个<see cref="CacheStatsInfo"/>类型的实例
        /// </summary>
        public CacheStatsInfo()
        {
            _counters = new ConcurrentDictionary<string, CacheStatsCounter>();
        }

        /// <summary>
        /// 缓存击中
        /// </summary>
        public void OnHit()
        {
            GetCounter().Increment(StatsType.Hit);
        }

        /// <summary>
        /// 缓存穿透
        /// </summary>
        public void OnMiss()
        {
            GetCounter().Increment(StatsType.Missed);
        }

        /// <summary>
        /// 获取统计值
        /// </summary>
        /// <param name="statsType">统计类型</param>
        /// <returns></returns>
        public long GetStatistic(StatsType statsType)
        {
            return GetCounter().Get(statsType);
        }

        /// <summary>
        /// 获取计数器
        /// </summary>
        /// <returns></returns>
        private CacheStatsCounter GetCounter()
        {
            if (!_counters.TryGetValue(DefaultKey, out var counter))
            {
                counter = new CacheStatsCounter();
                if (_counters.TryAdd(DefaultKey, counter))
                {
                    return counter;
                }
                return GetCounter();
            }
            return counter;
        }
    }
}
