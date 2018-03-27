using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Utils.IdGenerators.Snowflake
{
    /// <summary>
    /// 雪花算法
    /// </summary>
    public class IdWorker
    {
        /// <summary>
        /// 基准时间
        /// </summary>
        public const long Twepoch = 1288834974657L;

        /// <summary>
        /// 机器标识位数
        /// </summary>
        private const int WorkerIdBits = 5;

        /// <summary>
        /// 数据标志位数
        /// </summary>
        private const int DatacenterIdBits = 5;

        /// <summary>
        /// 序列号标识位数
        /// </summary>
        private const int SequenceBits = 12;

        /// <summary>
        /// 机器ID最大值
        /// </summary>
        private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);

        /// <summary>
        /// 数据标志最大值
        /// </summary>
        private const long MaxDatacenterId = -1L ^ (-1L << DatacenterIdBits);

        /// <summary>
        /// 序列号ID最大值
        /// </summary>
        private const long SequenceMask = -1L ^ (-1L << SequenceBits);

        /// <summary>
        /// 机器ID偏左移12位
        /// </summary>
        private const int WorkerIdShift = SequenceBits;

        /// <summary>
        /// 数据ID偏左移17位
        /// </summary>
        private const int DatacenterIdShift = SequenceBits + WorkerIdBits;

        /// <summary>
        /// 时间毫秒左移22位
        /// </summary>
        private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits;

        private long _sequence = 0L;

        private long _lastTimestamp = -1L;

        public long WorkerId { get; protected set; }

        public long DatacenterId { get; protected set; }

        public long Sequence
        {
            get { return _sequence; }
            internal set { _sequence = value; }
        }

        public IdWorker(long workerId, long datacenterId, long sequence = 0L)
        {
            // 如果超出范围就抛出异常
            if (workerId > MaxWorkerId || workerId < 0)
            {
                throw new ArgumentException($"worker Id 必须大于0，且不能大于 MaxWorkerId：{MaxWorkerId}");
            }

            if (datacenterId > MaxDatacenterId || datacenterId < 0)
            {
                throw new ArgumentException($"datacenter Id 必须大于0，且不能大于 MaxDatacenterId：{MaxDatacenterId}");
            }

            // 先校验再赋值
            WorkerId = workerId;
            DatacenterId = datacenterId;
            _sequence = sequence;
        }

        readonly object _lock=new object();
        public virtual long NextId()
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

                _lastTimestamp = timestamp;
                return ((timestamp - Twepoch) << TimestampLeftShift) | (DatacenterId << DatacenterIdShift) | (WorkerId << WorkerIdShift) | _sequence;
            }
        }

        /// <summary>
        /// 获取增量时间戳，防止产生的时间比之前的时间还要小（由于NTP回拨等问题），保持增量的趋势
        /// </summary>
        /// <param name="lastTimestamp">最后一个时间戳</param>
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
        /// 获取当前时间戳
        /// </summary>
        /// <returns></returns>
        protected virtual long TimeGen()
        {
            return CurrentTimeMills();
        }


        public static Func<long> CurrentTimeFunc = InternalCurrentTimeMillis;

        public static long CurrentTimeMills()
        {
            return CurrentTimeFunc();
        }

        public static IDisposable StubCurrentTime(Func<long> func)
        {
            CurrentTimeFunc = func;
            return new DisposableAction(() => { CurrentTimeFunc = InternalCurrentTimeMillis; });
        }


        public static IDisposable StubCurrentTime(long millis)
        {
            CurrentTimeFunc = () => millis;
            return new DisposableAction(() => { CurrentTimeFunc = InternalCurrentTimeMillis; });
        }

        private static readonly DateTime Jan1St1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static long InternalCurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Jan1St1970).TotalMilliseconds;
        }
    }
}
