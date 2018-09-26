using System.Threading;

namespace Bing.Caching.CacheStats
{
    /// <summary>
    /// 缓存统计信息计数器
    /// </summary>
    public class CacheStatsCounter
    {
        /// <summary>
        /// 计数器
        /// </summary>
        private readonly long[] _counters=new long[2];

        /// <summary>
        /// 增量
        /// </summary>
        /// <param name="statsType">统计类型</param>
        public void Increment(StatsType statsType)
        {
            Interlocked.Increment(ref _counters[(int) statsType]);
        }

        /// <summary>
        /// 获取计数
        /// </summary>
        /// <param name="statsType">统计类型</param>
        /// <returns></returns>
        public long Get(StatsType statsType)
        {
            return Interlocked.Read(ref _counters[(int) statsType]);
        }
    }
}
