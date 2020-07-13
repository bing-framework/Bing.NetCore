using System;
using System.Globalization;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 日期时间(<see cref="DateTime"/>) 扩展方法
    /// </summary>
    public static partial class DateTimeExtensions
    {
        /// <summary>
        /// 获取年份的第一天
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime FirstDayOfYear(this DateTime dt) => dt.SetDate(dt.Year, 1, 1);

        /// <summary>
        /// 获取季度的第一天
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime FirstDayOfQuarter(this DateTime dt)
        {
            var currentQuarter = (dt.Month - 1) / 3 + 1;
            var firstDay = new DateTime(dt.Year, 3 * currentQuarter - 2, 1);
            return dt.SetDate(firstDay.Year, firstDay.Month, firstDay.Day);
        }

        /// <summary>
        /// 获取月份的第一天
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime FirstDayOfMonth(this DateTime dt) => dt.SetDay(1);

        /// <summary>
        /// 获取星期的第一天
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime FirstDayOfWeek(this DateTime dt)
        {
            var currentCulture = CultureInfo.CurrentCulture;
            var firstDayOfWeek = currentCulture.DateTimeFormat.FirstDayOfWeek;
            var offset = dt.DayOfWeek - firstDayOfWeek < 0 ? 7 : 0;
            var numberOfDaysSinceBeginningOfTheWeek = dt.DayOfWeek + offset - firstDayOfWeek;
            return dt.AddDays(-numberOfDaysSinceBeginningOfTheWeek);
        }

        /// <summary>
        /// 获取年份的最后一天
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime LastDayOfYear(this DateTime dt) => dt.SetDate(dt.Year, 12, 31);

        /// <summary>
        /// 获取季度的最后一天
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime LastDayOfQuarter(this DateTime dt)
        {
            var currentQuarter = (dt.Month - 1) / 3 + 1;
            var firstDay = new DateTime(dt.Year, 3 * currentQuarter - 2, 1);
            return firstDay.SetMonth(firstDay.Month + 2).LastDayOfMonth();
        }

        /// <summary>
        /// 获取月份的最后一天
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime LastDayOfMonth(this DateTime dt) => dt.SetDay(DateTime.DaysInMonth(dt.Year, dt.Month));

        /// <summary>
        /// 获取星期的最后一天
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime LastDayOfWeek(this DateTime dt) => dt.FirstDayOfWeek().AddDays(6);
    }
}
