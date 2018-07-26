using Bing.Sequence.Abstractions;

namespace Bing.Sequence.Snowflake
{
    /// <summary>
    /// 雪花算法序列号生成器构建器
    /// </summary>
    public class SnowflakeSequenceBuilder:ISequenceBuilder
    {
        /// <summary>
        /// 数据中心ID，值的范围在[0,31]之间，一般可以设置机房的IDC[必选]
        /// </summary>
        private long _datacenterId;

        /// <summary>
        /// 工作机器ID，值的范围在[0,31]之间，一般可以设置及其编号[必选]
        /// </summary>
        private long _workerId;

        /// <summary>
        /// 序列号
        /// </summary>
        private long _sequence;

        /// <summary>
        /// 构建一个序列号生成器
        /// </summary>
        /// <returns></returns>
        public ISequence Build()
        {
            SnowflakeSequence sequence = new SnowflakeSequence(_workerId, _datacenterId, _sequence);
            return sequence;
        }

        /// <summary>
        /// 创建序列号生成器构建器
        /// </summary>
        /// <param name="datacenterId">数据中心ID</param>
        /// <param name="workerId">工作机器ID</param>
        /// <param name="sequence">序列号</param>
        /// <returns></returns>
        public static SnowflakeSequenceBuilder Create(long datacenterId,long workerId,long sequence=0L)
        {
            SnowflakeSequenceBuilder builder = new SnowflakeSequenceBuilder();
            builder._datacenterId = datacenterId;
            builder._workerId = workerId;
            builder._sequence = sequence;
            return builder;
        }        
    }
}
