using System;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 单精度浮点型(<see cref="Single"/>) 扩展
    /// </summary>
    public static class FloatExtensions
    {
        #region InRange(判断值是否在指定范围内)

        /// <summary>
        /// 判断当前值是否在指定范围内
        /// </summary>
        /// <param name="value">float</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        public static bool InRange(this float value, float minValue, float maxValue) => (value >= minValue && value <= maxValue);

        /// <summary>
        /// 判断值是否在指定范围内，否则返回默认值
        /// </summary>
        /// <param name="value">float</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        /// <param name="defaultValue">默认值</param>
        public static float InRange(this float value, float minValue, float maxValue, float defaultValue) => value.InRange(minValue, maxValue) ? value : defaultValue;

        #endregion

        #region Days(获取日期间隔)

        /// <summary>
        /// 获取日期间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="days">float</param>
        public static TimeSpan Days(this float days) => TimeSpan.FromDays(days);

        #endregion

        #region Hours(获取小时间隔)

        /// <summary>
        /// 获取小时间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="hours">float</param>
        public static TimeSpan Hours(this float hours) => TimeSpan.FromHours(hours);

        #endregion

        #region Minutes(获取分钟间隔)

        /// <summary>
        /// 获取分钟间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="minutes">float</param>
        public static TimeSpan Minutes(this float minutes) => TimeSpan.FromMinutes(minutes);

        #endregion

        #region Seconds(获取秒间隔)

        /// <summary>
        /// 获取秒间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="seconds">float</param>
        public static TimeSpan Seconds(this float seconds) => TimeSpan.FromSeconds(seconds);

        #endregion

        #region Milliseconds(获取毫秒间隔)

        /// <summary>
        /// 获取毫秒间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="milliseconds">float</param>
        public static TimeSpan Milliseconds(this float milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

        #endregion
    }
}
