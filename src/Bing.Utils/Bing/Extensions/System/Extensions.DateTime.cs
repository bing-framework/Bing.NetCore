using System;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 基础类型扩展
    /// </summary>
    public static partial class BaseTypeExtensions
    {
        #region SetDateTime(设置时间)

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="date">DateTime</param>
        /// <param name="hours">时</param>
        /// <param name="minutes">分</param>
        /// <param name="seconds">秒</param>
        public static DateTime SetDateTime(this DateTime date, int hours, int minutes, int seconds) =>
            date.SetDateTime(new TimeSpan(hours, minutes, seconds));

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="date">DateTime</param>
        /// <param name="time">时间跨度</param>
        public static DateTime SetDateTime(this DateTime date, TimeSpan time) => date.Date.Add(time);

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="date">DateTimeOffset</param>
        /// <param name="hours">时</param>
        /// <param name="minutes">分</param>
        /// <param name="seconds">秒</param>
        public static DateTimeOffset SetDateTime(this DateTimeOffset date, int hours, int minutes, int seconds) =>
            date.SetDateTime(new TimeSpan(hours, minutes, seconds));

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="date">DateTimeOffset</param>
        /// <param name="time">时间跨度</param>
        public static DateTimeOffset SetDateTime(this DateTimeOffset date, TimeSpan time) => date.SetDateTime(time, null);

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="date">DateTimeOffset</param>
        /// <param name="time">时间跨度</param>
        /// <param name="localTimeZone">时区</param>
        public static DateTimeOffset SetDateTime(this DateTimeOffset date, TimeSpan time, TimeZoneInfo localTimeZone) =>
            date.ToLocalDateTime(localTimeZone).SetDateTime(time).ToDateTimeOffset(localTimeZone);

        #endregion

        /// <summary>
        /// 将时间转换为时间点
        /// </summary>
        /// <param name="localDateTime">DateTime</param>
        public static DateTimeOffset ToDateTimeOffset(this DateTime localDateTime) =>
            localDateTime.ToDateTimeOffset(null);

        /// <summary>
        /// 将时间转换为时间点
        /// </summary>
        /// <param name="localDateTime">DateTime</param>
        /// <param name="localTimeZone">时区</param>
        public static DateTimeOffset ToDateTimeOffset(this DateTime localDateTime, TimeZoneInfo localTimeZone)
        {
            if (localDateTime.Kind != DateTimeKind.Unspecified)
                localDateTime = new DateTime(localDateTime.Ticks, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTime(localDateTime, localTimeZone ?? TimeZoneInfo.Local);
        }

        /// <summary>
        /// 将时间点转换为时间
        /// </summary>
        /// <param name="dateTimeUtc">DateTimeOffset</param>
        public static DateTime ToLocalDateTime(this DateTimeOffset dateTimeUtc) => dateTimeUtc.ToLocalDateTime(null);

        /// <summary>
        /// 将时间点转换为时间
        /// </summary>
        /// <param name="dateTimeUtc">DateTimeOffset</param>
        /// <param name="localTimeZone">时区</param>
        public static DateTime ToLocalDateTime(this DateTimeOffset dateTimeUtc, TimeZoneInfo localTimeZone) =>
            TimeZoneInfo.ConvertTime(dateTimeUtc, localTimeZone ?? TimeZoneInfo.Local).DateTime;
    }
}
