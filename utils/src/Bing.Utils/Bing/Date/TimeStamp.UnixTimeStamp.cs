using System;

namespace Bing.Date
{
    /// <summary>
    /// Unix 时间戳
    /// </summary>
    public class UnixTimeStamp : TimeStamp
    {
        /// <summary>
        /// 初始化一个<see cref="UnixTimeStamp"/>类型的实例
        /// </summary>
        public UnixTimeStamp() : this(DateTime.Now) { }

        /// <summary>
        /// 初始化一个<see cref="UnixTimeStamp"/>类型的实例
        /// </summary>
        /// <param name="timestamp">Unix时间戳</param>
        public UnixTimeStamp(long timestamp) : this(FromUnixTimestampFunc(timestamp), timestamp) { }

        /// <summary>
        /// 初始化一个<see cref="UnixTimeStamp"/>类型的实例
        /// </summary>
        /// <param name="dt">时间</param>
        public UnixTimeStamp(DateTime dt) : this(dt, ToUnixTimestampFunc(dt)) { }

        /// <summary>
        /// 初始化一个<see cref="UnixTimeStamp"/>类型的实例
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="timestamp">Unix时间戳</param>
        private UnixTimeStamp(DateTime dt, long timestamp)
        {
            m_datetime = dt;
            m_timestamp = timestamp;
        }

        /// <summary>
        /// 转换为时间。根据Unix时间戳获取相应的时间
        /// </summary>
        public override DateTime ToDateTime() => m_datetime;

        /// <summary>
        /// 转换为 Unix时间戳。根据时间获取相应的时间戳
        /// </summary>
        public override long ToTimestamp() => m_timestamp;

        /// <summary>
        /// 时间转Unix时间戳函数
        /// </summary>
        private static readonly Func<DateTime, long> ToUnixTimestampFunc = time => (time.ToUniversalTime().Ticks - 621355968000000000) / 10000000;

        /// <summary>
        /// Unix时间戳转时间函数
        /// </summary>
        private static readonly Func<long, DateTime> FromUnixTimestampFunc = timestamp =>
            (TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local)).Add(
                new TimeSpan(long.Parse(timestamp + "0000000")));

        /// <summary>
        /// 当前Unix时间戳
        /// </summary>
        public static Func<long> NowUnixTimeStamp = () => ToUnixTimestampFunc(DateTime.Now);

        /// <summary>
        /// 当前Unix时间戳(UTC)
        /// </summary>
        public static Func<long> UtcNowUnixTimeStamp = () => ToUnixTimestampFunc(DateTime.UtcNow);
    }
}
