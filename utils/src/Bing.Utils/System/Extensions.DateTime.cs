using Bing.Date;

namespace System
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
    }
}
