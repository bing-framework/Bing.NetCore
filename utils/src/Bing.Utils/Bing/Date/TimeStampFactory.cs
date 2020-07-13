using System;

namespace Bing.Date
{
    /// <summary>
    /// 时间戳工厂
    /// </summary>
    public class TimeStampFactory
    {
        /// <summary>
        /// 最后值
        /// </summary>
        private DateTime _lastValue = DateTime.MinValue;

        /// <summary>
        /// 对象锁
        /// </summary>
        private readonly object _lockObj = new object();

        /// <summary>
        /// 毫秒增量
        /// </summary>
        public double IncrementMs { get; set; } = 4;

        /// <summary>
        /// 获取时间
        /// </summary>
        public DateTime GetDateTime() => GetDateTimeCore(DateTime.Now);

        /// <summary>
        /// 获取时间(UTC)
        /// </summary>
        public DateTime GetUtcDateTime() => GetDateTimeCore(DateTime.UtcNow);

        /// <summary>
        /// 获取时间戳
        /// </summary>
        public TimeStamp GetTimeStamp() => new TimeStamp(GetDateTime());

        /// <summary>
        /// 获取时间戳(UTC)
        /// </summary>
        public TimeStamp GetUtcTimeStamp() => new TimeStamp(GetUtcDateTime());

        /// <summary>
        /// 获取Unix时间戳
        /// </summary>
        public UnixTimeStamp GetUnixTimeStamp() => new UnixTimeStamp(GetDateTime());

        /// <summary>
        /// 获取Unix时间戳(UTC)
        /// </summary>
        public UnixTimeStamp GetUtcUnixTimeStamp() => new UnixTimeStamp(GetUtcDateTime());

        /// <summary>
        /// 获取时间核心方法
        /// </summary>
        /// <param name="refDt">引用时间</param>
        private DateTime GetDateTimeCore(DateTime refDt)
        {
            var now = refDt;
            lock (_lockObj)
            {
                if ((now - _lastValue).TotalMilliseconds < IncrementMs)
                    now = _lastValue.AddMilliseconds(IncrementMs);
                _lastValue = now;
            }
            return now;
        }
    }
}
