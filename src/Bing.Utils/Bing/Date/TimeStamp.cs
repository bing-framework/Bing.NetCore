using System;

namespace Bing.Date
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public class TimeStamp
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        // ReSharper disable once InconsistentNaming
        protected long m_timestamp;

        /// <summary>
        /// 时间
        /// </summary>
        // ReSharper disable once InconsistentNaming
        protected DateTime m_datetime;

        /// <summary>
        /// 初始化一个<see cref="TimeStamp"/>类型的实例
        /// </summary>
        public TimeStamp() : this(DateTime.Now) { }

        /// <summary>
        /// 初始化一个<see cref="TimeStamp"/>类型的实例
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        public TimeStamp(long timestamp) : this(FromTimestampFunc(timestamp), timestamp) { }

        /// <summary>
        /// 初始化一个<see cref="TimeStamp"/>类型的实例
        /// </summary>
        /// <param name="dt">时间</param>
        public TimeStamp(DateTime dt) : this(dt, ToTimestampFunc(dt)) { }

        /// <summary>
        /// 初始化一个<see cref="TimeStamp"/>类型的实例
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="timestamp">时间戳</param>
        private TimeStamp(DateTime dt, long timestamp)
        {
            m_datetime = dt;
            m_timestamp = timestamp;
        }

        /// <summary>
        /// 转换为时间。根据时间戳获取相应的时间
        /// </summary>
        public virtual DateTime ToDateTime() => m_datetime;

        /// <summary>
        /// 转换为时间戳。根据时间获取相应的时间戳
        /// </summary>
        public virtual long ToTimestamp() => m_timestamp;

        /// <summary>
        /// 时间转换时间戳函数
        /// </summary>
        private static readonly Func<DateTime, long> ToTimestampFunc = time => time.Ticks;

        /// <summary>
        /// 时间戳转时间函数
        /// </summary>
        private static readonly Func<long, DateTime> FromTimestampFunc = timestamp => new DateTime(timestamp);

        /// <summary>
        /// 当前时间戳
        /// </summary>
        public static Func<long> NowTimeStamp = () => ToTimestampFunc(DateTime.Now);

        /// <summary>
        /// 当前时间戳(UTC)
        /// </summary>
        public static Func<long> UtcNowTimeStamp = () => ToTimestampFunc(DateTime.UtcNow);
    }
}
