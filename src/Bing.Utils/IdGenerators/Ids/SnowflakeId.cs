using System;

namespace Bing.Utils.IdGenerators.Ids
{
    /// <summary>
    /// 雪花算法，代码出自：https://github.com/dunitian/snowflake-net/blob/master/Snowflake.Net.Core/IdWorker.cs
    /// </summary>
    public class SnowflakeId
    {
        /// <summary>
        /// 基准时间
        /// </summary>
        public const long TWEPOCH = 1288834974657L;

        /// <summary>
        /// 机器标识位数
        /// </summary>
        private const int WORKER_ID_BITS = 5;

        /// <summary>
        /// 数据标志位数
        /// </summary>
        private const int DATACENTER_ID_BITS = 5;

        /// <summary>
        /// 序列号标识位数
        /// </summary>
        private const int SEQUENCE_BITS = 12;

        /// <summary>
        /// 机器ID最大值
        /// </summary>
        private const long MAX_WORKER_ID = -1L ^ (-1L << WORKER_ID_BITS);

        /// <summary>
        /// 数据标志最大值
        /// </summary>
        private const long MAX_DATACENTER_ID = -1L ^ (-1L << DATACENTER_ID_BITS);

        /// <summary>
        /// 序列号ID最大值
        /// </summary>
        private const long SEQUENCE_MASK = -1L ^ (-1L << SEQUENCE_BITS);

        /// <summary>
        /// 机器ID偏左移12位
        /// </summary>
        private const int WORKER_ID_SHIFT = SEQUENCE_BITS;

        /// <summary>
        /// 数据ID偏左移17位
        /// </summary>
        private const int DATACENTER_ID_SHIFT = SEQUENCE_BITS + WORKER_ID_BITS;

        /// <summary>
        /// 时间毫秒左移22位
        /// </summary>
        private const int TIMESTAMP_LEFT_SHIFT = SEQUENCE_BITS + WORKER_ID_BITS + DATACENTER_ID_BITS;

        /// <summary>
        /// 序列号ID
        /// </summary>
        private long _sequence = 0L;

        /// <summary>
        /// 最后时间戳
        /// </summary>
        private long _lastTimestamp = -1L;

        /// <summary>
        /// 机器ID
        /// </summary>
        public long WorkerId { get; protected set; }

        /// <summary>
        /// 数据标志ID
        /// </summary>
        public long DatacenterId { get; protected set; }

        /// <summary>
        /// 序列号ID
        /// </summary>
        public long Sequence
        {
            get => _sequence;
            internal set => _sequence = value;
        }

        /// <summary>
        /// 初始化一个<see cref="SnowflakeId"/>类型的实例
        /// </summary>
        /// <param name="workerId">机器ID</param>
        /// <param name="datacenterId">数据标志ID</param>
        /// <param name="sequence">序列号ID</param>
        public SnowflakeId(long workerId, long datacenterId, long sequence = 0L)
        {
            // 如果超出范围就抛出异常
            if (workerId > MAX_WORKER_ID || workerId < 0)
            {
                throw new ArgumentException($"worker Id 必须大于0，且不能大于 MaxWorkerId：{MAX_WORKER_ID}");
            }

            if (datacenterId > MAX_DATACENTER_ID || datacenterId < 0)
            {
                throw new ArgumentException($"datacenter Id 必须大于0，且不能大于 MaxDatacenterId：{MAX_DATACENTER_ID}");
            }

            // 先校验再赋值
            WorkerId = workerId;
            DatacenterId = datacenterId;
            _sequence = sequence;
        }

        /// <summary>
        /// 对象锁
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// 获取下一个ID
        /// </summary>
        /// <returns></returns>
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
                    _sequence = (_sequence + 1) & SEQUENCE_MASK;
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
                return ((timestamp - TWEPOCH) << TIMESTAMP_LEFT_SHIFT) | (DatacenterId << DATACENTER_ID_SHIFT) | (WorkerId << WORKER_ID_SHIFT) | _sequence;
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
            while (timestamp <= lastTimestamp)
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

        /// <summary>
        /// 获取当前时间戳
        /// </summary>
        public static Func<long> CurrentTimeFunc = InternalCurrentTimeMillis;

        /// <summary>
        /// 获取当前时间戳
        /// </summary>
        /// <returns></returns>
        public static long CurrentTimeMills()
        {
            return CurrentTimeFunc();
        }

        /// <summary>
        /// 重置当前时间戳
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public static IDisposable StubCurrentTime(Func<long> func)
        {
            CurrentTimeFunc = func;
            return new DisposableAction(() => { CurrentTimeFunc = InternalCurrentTimeMillis; });
        }

        /// <summary>
        /// 重置当前时间戳
        /// </summary>
        /// <param name="millis"></param>
        /// <returns></returns>
        public static IDisposable StubCurrentTime(long millis)
        {
            CurrentTimeFunc = () => millis;
            return new DisposableAction(() => { CurrentTimeFunc = InternalCurrentTimeMillis; });
        }

        private static readonly DateTime Jan1St1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 默认当前时间戳
        /// </summary>
        /// <returns></returns>
        private static long InternalCurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Jan1St1970).TotalMilliseconds;
        }

        /// <summary>
        /// 一次性方法
        /// </summary>
        public class DisposableAction : IDisposable
        {
            /// <summary>
            /// 执行方法
            /// </summary>
            private readonly Action _action;

            /// <summary>
            /// 初始化一个<see cref="DisposableAction"/>类型的实例
            /// </summary>
            /// <param name="action">执行方法</param>
            public DisposableAction(Action action)
            {
                _action = action ?? throw new ArgumentNullException(nameof(action));
            }

            /// <summary>
            /// 释放资源
            /// </summary>
            public void Dispose()
            {
                _action();
            }
        }
    }
}
