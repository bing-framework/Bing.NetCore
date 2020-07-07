using System;

namespace Bing.Judgments
{
    /// <summary>
    /// 时间(<see cref="DateTime"/>) 判断
    /// </summary>
    public static class DateTimeJudgment
    {
        /// <summary>
        /// 最小日期
        /// </summary>
        internal static readonly DateTime MinDate = new DateTime(1900, 1, 1);

        /// <summary>
        /// 最大日期
        /// </summary>
        internal static readonly DateTime MaxDate = new DateTime(9999, 12, 31, 23, 59, 59, 999);

        #region IsToday(是否今天)

        /// <summary>
        /// 判断指定时间是否今天
        /// </summary>
        /// <param name="dt">时间</param>
        public static bool IsToday(DateTime dt) => dt.Date == DateTime.Today;

        /// <summary>
        /// 判断指定时间是否今天
        /// </summary>
        /// <param name="dt">时间</param>
        public static bool IsToday(DateTime? dt) => IsToday(dt.GetValueOrDefault());

        /// <summary>
        /// 判断指定时间是否今天
        /// </summary>
        /// <param name="dtOffset">时间偏移</param>
        public static bool IsToday(DateTimeOffset dtOffset) => IsToday(dtOffset.Date);

        /// <summary>
        /// 判断指定时间是否今天
        /// </summary>
        /// <param name="dtOffset">时间偏移</param>
        public static bool IsToday(DateTimeOffset? dtOffset) => IsToday(dtOffset.GetValueOrDefault());

        #endregion

        #region IsWeekend(是否周末)

        /// <summary>
        /// 判断指定时间是否周末
        /// </summary>
        /// <param name="dt">时间</param>
        public static bool IsWeekend(DateTime dt) => dt.DayOfWeek == DayOfWeek.Sunday || dt.DayOfWeek == DayOfWeek.Saturday;

        /// <summary>
        /// 判断指定时间是否周末
        /// </summary>
        /// <param name="dt">时间</param>
        public static bool IsWeekend(DateTime? dt) => IsWeekend(dt.GetValueOrDefault());

        /// <summary>
        /// 判断指定时间是否周末
        /// </summary>
        /// <param name="dtOffset">时间偏移</param>
        public static bool IsWeekend(DateTimeOffset dtOffset) => IsWeekend(dtOffset.Date);

        /// <summary>
        /// 判断指定时间是否周末
        /// </summary>
        /// <param name="dtOffset">时间偏移</param>
        public static bool IsWeekend(DateTimeOffset? dtOffset) => IsWeekend(dtOffset.GetValueOrDefault());

        #endregion

        #region IsValid(是否有效时间)

        /// <summary>
        /// 判断指定时间是否有效时间
        /// </summary>
        /// <param name="dt">时间</param>
        public static bool IsValid(DateTime dt) => dt >= MinDate && dt <= MaxDate;

        #endregion
    }
}
