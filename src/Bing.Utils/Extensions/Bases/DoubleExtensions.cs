using System;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 双精度浮点型(<see cref="Double"/>) 扩展
    /// </summary>
    public static class DoubleExtensions
    {
        #region InRange(判断值是否在指定范围内)

        /// <summary>
        /// 判断当前值是否在指定范围内
        /// </summary>
        /// <param name="value">double</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        /// <returns>bool</returns>
        public static bool InRange(this double value, double minValue, double maxValue)
        {
            return (value >= minValue && value <= maxValue);
        }

        /// <summary>
        /// 判断值是否在指定范围内，否则返回默认值
        /// </summary>
        /// <param name="value">double</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>double</returns>
        public static double InRange(this double value, double minValue, double maxValue, double defaultValue)
        {
            return value.InRange(minValue, maxValue) ? value : defaultValue;
        }

        #endregion

        #region Days(获取日期间隔)

        /// <summary>
        /// 获取日期间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="days">double</param>
        /// <returns>日期间隔</returns>
        public static TimeSpan Days(this double days)
        {
            return TimeSpan.FromDays(days);
        }

        #endregion

        #region Hours(获取小时间隔)

        /// <summary>
        /// 获取小时间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="hours">double</param>
        /// <returns>小时间隔</returns>
        public static TimeSpan Hours(this double hours)
        {
            return TimeSpan.FromHours(hours);
        }

        #endregion

        #region Minutes(获取分钟间隔)

        /// <summary>
        /// 获取分钟间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="minutes">double</param>
        /// <returns>分钟间隔</returns>
        public static TimeSpan Minutes(this double minutes)
        {
            return TimeSpan.FromMinutes(minutes);
        }

        #endregion

        #region Seconds(获取秒间隔)

        /// <summary>
        /// 获取秒间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="seconds">double</param>
        /// <returns>秒间隔</returns>
        public static TimeSpan Seconds(this double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        #endregion

        #region Milliseconds(获取毫秒间隔)

        /// <summary>
        /// 获取毫秒间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="milliseconds">double</param>
        /// <returns>毫秒间隔</returns>
        public static TimeSpan Milliseconds(this double milliseconds)
        {
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        #endregion
    }
}
