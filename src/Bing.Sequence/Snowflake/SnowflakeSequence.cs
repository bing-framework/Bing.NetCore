using System;
using Bing.Sequence.Abstractions;

namespace Bing.Sequence.Snowflake
{
    /// <summary>
    /// 雪花算法序列号生成器
    /// </summary>
    public class SnowflakeSequence:ISequence
    {
        /// <summary>
        /// 基准时间
        /// </summary>
        public const long TWEPOCH = 1288834974657L;

        /// <summary>
        /// 机器ID所占的位数
        /// </summary>
        private const int WorkerIdBits = 5;

        /// <summary>
        /// 数据标志ID所占的位数
        /// </summary>
        private const int DatacenterIdBits = 5;

        /// <summary>
        /// 序列在ID中占的位置
        /// </summary>
        private const int SequenceBits = 12;

        /// <summary>
        /// 支持的最大机器ID，结果是31（这个移位算法可以很快的计算出几位二进制数所能表示的最大十进制数）
        /// </summary>
        private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);

        /// <summary>
        /// 支持的最大数据标识ID，结果是31
        /// </summary>
        private const long MaxDatacenterId = -1L ^ (-1L << DatacenterIdBits);

        /// <summary>
        /// 生成序列的掩码，这里为4095(0b111111111111=0xfff=4095)
        /// </summary>
        private const long SequenceMask = -1L ^ (-1L << SequenceBits);

        /// <summary>
        /// 机器ID向左移12位
        /// </summary>
        private const int WorkerIdShift = SequenceBits;

        /// <summary>
        /// 数据标识ID向左移17位(12+5)
        /// </summary>
        private const int DatacenterIdShift = SequenceBits + WorkerIdBits;

        /// <summary>
        /// 时间戳向左移22位(5+5+12)
        /// </summary>
        private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits;

        /// <summary>
        /// 工作机器ID(0-31)
        /// </summary>
        public long WorkerId { get; protected set; }

        /// <summary>
        /// 数据中心ID(0-31)
        /// </summary>
        public long DatacenterId { get; protected set; }

        /// <summary>
        /// 序列号
        /// </summary>
        private long _sequence = 0L;

        /// <summary>
        /// 上次生成ID的时间戳
        /// </summary>
        private long _lastTimestamp = -1L;

        /// <summary>
        /// 毫秒内序列(0-4095)
        /// </summary>
        public long Sequence
        {
            get => _sequence;
            internal set => _sequence = value;
        }

        /// <summary>
        /// 对象锁
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// 初始化一个<see cref="SnowflakeSequence"/>类型的实例
        /// </summary>
        /// <param name="workerId">工作机器ID</param>
        /// <param name="datacenterId">数据标识ID</param>
        /// <param name="sequence">序列号</param>
        public SnowflakeSequence(long workerId, long datacenterId, long sequence = 0L)
        {
            // 如果超出范围就抛出异常
            if (workerId > MaxWorkerId || workerId < 0)
            {
                throw new ArgumentOutOfRangeException($"worker Id 必须大于0，且不能大于 MaxWorkerId：{MaxWorkerId}");
            }

            if (datacenterId > MaxDatacenterId || datacenterId < 0)
            {
                throw new ArgumentOutOfRangeException($"datacenter Id 必须大于0，且不能大于 MaxDatacenterId：{MaxDatacenterId}");
            }

            // 先校验再赋值
            WorkerId = workerId;
            DatacenterId = datacenterId;
            _sequence = sequence;
        }

        /// <summary>
        /// 生成下一个序列号
        /// </summary>
        /// <returns></returns>
        public long NextValue()
        {
            lock (_lock)
            {
                var timestamp = TimeGen();
                if (timestamp < _lastTimestamp)
                {
                    throw new Exception($"时间戳必须大于上一次生成ID的时间戳，拒绝为{_lastTimestamp - timestamp}毫秒生成id");
                }

                // 如果上次生成时间和当前时间相同，在同一毫秒内
                if (_lastTimestamp == timestamp)
                {
                    // sequence自增，和sequenceMask相与一下，去掉高位
                    _sequence = (_sequence + 1) & SequenceMask;
                    //判断是否溢出,也就是每毫秒内超过1024，当为1024时，与sequenceMask相与，sequence就等于0
                    if (_sequence == 0)
                    {
                        //等待到下一毫秒
                        timestamp = TilNextMillis(_lastTimestamp);
                    }
                }
                else
                {
                    //如果和上次生成时间不同,重置sequence，就是下一毫秒开始，sequence计数重新从0开始累加,
                    //为了保证尾数随机性更大一些,最后一位可以设置一个随机数
                    _sequence = 0;//new Random().Next(10);
                }
                // 上次生成ID的时间戳
                _lastTimestamp = timestamp;
                // 移位并通过或运算拼到一起组成64位的ID
                return ((timestamp - TWEPOCH) << TimestampLeftShift) | (DatacenterId << DatacenterIdShift) | (WorkerId << WorkerIdShift) | _sequence;
            }
        }

        /// <summary>
        /// 获取增量时间戳，防止产生的时间比之前的时间还要小（由于NTP回拨等问题），保持增量的趋势
        /// </summary>
        /// <param name="lastTimestamp">上次生成ID的时间戳</param>
        /// <returns></returns>
        protected virtual long TilNextMillis(long lastTimestamp)
        {
            var timestamp = TimeGen();
            while (timestamp<=lastTimestamp)
            {
                timestamp = TimeGen();
            }

            return timestamp;
        }

        /// <summary>
        /// 返回以毫秒为单位的当前时间
        /// </summary>
        /// <returns></returns>
        protected virtual long TimeGen()
        {
            return CurrentTimeFunc();
        }

        /// <summary>
        /// 获取当前时间戳
        /// </summary>
        public static Func<long> CurrentTimeFunc = InternalCurrentTimeMillis;

        /// <summary>
        /// 当前时间戳
        /// </summary>
        /// <returns></returns>
        public static long CurrentTimeMills()
        {
            return CurrentTimeFunc();
        }

        private static readonly DateTime Jan1St1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 默认当前时间戳
        /// </summary>
        /// <returns></returns>
        private static long InternalCurrentTimeMillis()
        {
            return (long) (DateTime.UtcNow - Jan1St1970).TotalMilliseconds;
        }
    }
}
