using System;
using Bing.Utils.Timing;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 时间操作
    /// </summary>
    public static class Time
    {
        /// <summary>
        /// 日期
        /// </summary>
        private static DateTime? _dateTime;

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="dateTime">时间</param>
        public static void SetTime(DateTime? dateTime) => _dateTime = dateTime;

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="dateTime">时间</param>
        public static void SetTime(string dateTime) => _dateTime = Conv.ToDateOrNull(dateTime);

        /// <summary>
        /// 重置时间
        /// </summary>
        public static void Reset() => _dateTime = null;

        /// <summary>
        /// 获取当前日期时间
        /// </summary>
        public static DateTime GetDateTime() => _dateTime ?? DateTime.Now;

        /// <summary>
        /// 获取当前日期，不带时间
        /// </summary>
        public static DateTime GetDate() => GetDateTime().Date;

        /// <summary>
        /// 获取Unix时间戳
        /// </summary>
        public static long GetUnixTimestamp() => GetUnixTimestamp(DateTime.Now);

        /// <summary>
        /// 获取Unix时间戳
        /// </summary>
        /// <param name="time">时间</param>
        public static long GetUnixTimestamp(DateTime time)
        {
            var start = TimeZoneInfo.ConvertTime(DateTimeExtensions.Date1970, TimeZoneInfo.Local);
            var ticks = (time - start.Add(new TimeSpan(8, 0, 0))).Ticks;
            return Conv.ToLong(ticks / TimeSpan.TicksPerSecond);
        }

        /// <summary>
        /// 从Unix时间戳获取时间
        /// </summary>
        /// <param name="timestamp">Unix时间戳</param>
        public static DateTime GetTimeFromUnixTimestamp(long timestamp)
        {
            var start = TimeZoneInfo.ConvertTime(DateTimeExtensions.Date1970, TimeZoneInfo.Local);
            var span = new TimeSpan(long.Parse(timestamp + "0000000"));
            return start.Add(span).Add(new TimeSpan(8, 0, 0));
        }

        /// <summary>
        /// 格式化时间间隔
        /// </summary>
        /// <param name="span">时间间隔</param>
        public static string Format(TimeSpan span)
        {

        }
    }
}
