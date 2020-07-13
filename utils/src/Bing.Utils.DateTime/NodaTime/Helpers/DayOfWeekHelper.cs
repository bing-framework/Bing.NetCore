using System;

namespace NodaTime.Helpers
{
    /// <summary>
    /// 星期几(<see cref="DayOfWeek"/>) 操作辅助类
    /// </summary>
    internal static class DayOfWeekHelper
    {
        /// <summary>
        /// 转换为 NodaTime 星期几
        /// </summary>
        /// <param name="week">系统星期几</param>
        public static IsoDayOfWeek ToNodaTimeWeek(DayOfWeek week) =>
            week switch
            {
                DayOfWeek.Sunday => IsoDayOfWeek.Sunday,
                DayOfWeek.Monday => IsoDayOfWeek.Monday,
                DayOfWeek.Tuesday => IsoDayOfWeek.Tuesday,
                DayOfWeek.Wednesday => IsoDayOfWeek.Wednesday,
                DayOfWeek.Thursday => IsoDayOfWeek.Thursday,
                DayOfWeek.Friday => IsoDayOfWeek.Friday,
                DayOfWeek.Saturday => IsoDayOfWeek.Saturday,
                _ => IsoDayOfWeek.None
            };

        /// <summary>
        /// 转换为 系统星期几
        /// </summary>
        /// <param name="week">NodaTime 星期几</param>
        public static DayOfWeek ToSystemWeek(IsoDayOfWeek week) =>
            week switch
            {
                IsoDayOfWeek.Sunday => DayOfWeek.Sunday,
                IsoDayOfWeek.Monday => DayOfWeek.Monday,
                IsoDayOfWeek.Tuesday => DayOfWeek.Tuesday,
                IsoDayOfWeek.Wednesday => DayOfWeek.Wednesday,
                IsoDayOfWeek.Thursday => DayOfWeek.Thursday,
                IsoDayOfWeek.Friday => DayOfWeek.Friday,
                IsoDayOfWeek.Saturday => DayOfWeek.Saturday,
                _ => throw new InvalidOperationException("Unknown day of week")
            };
    }
}
