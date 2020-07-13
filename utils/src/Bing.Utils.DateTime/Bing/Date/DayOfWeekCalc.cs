using System;
using Bing.Extensions;
using NodaTime;
using NodaTime.Helpers;

namespace Bing.Date
{
    /// <summary>
    /// 星期计算
    /// </summary>
    public static class DayOfWeekCalc
    {
        /// <summary>
        /// 间隔天数
        /// </summary>
        /// <param name="left">星期几</param>
        /// <param name="right">星期几</param>
        public static int DaysBetween(DayOfWeek left, DayOfWeek right)
        {
            var leftVal = left.ToInt();
            var rightVal = right.ToInt();

            if (leftVal <= rightVal)
                return rightVal - leftVal;
            return leftVal + 7 - rightVal;
        }

        /// <summary>
        /// 间隔天数
        /// </summary>
        /// <param name="left">星期几</param>
        /// <param name="right">星期几</param>
        public static int DaysBetween(IsoDayOfWeek left, IsoDayOfWeek right)
        {
            if (left == IsoDayOfWeek.None || right == IsoDayOfWeek.None)
                return 0;
            return DaysBetween(DayOfWeekHelper.ToSystemWeek(left), DayOfWeekHelper.ToSystemWeek(right));
        }

        /// <summary>
        /// 尝试获取间隔天数
        /// </summary>
        /// <param name="left">星期几</param>
        /// <param name="right">星期几</param>
        /// <param name="days">间隔天数</param>
        public static bool TryDaysBetween(DayOfWeek left, DayOfWeek right, out int days)
        {
            days = DaysBetween(left, right);
            return true;
        }

        /// <summary>
        /// 尝试获取间隔天数
        /// </summary>
        /// <param name="left">星期几</param>
        /// <param name="right">星期几</param>
        /// <param name="days">间隔天数</param>
        public static bool TryDaysBetween(IsoDayOfWeek left, IsoDayOfWeek right, out int days)
        {
            days = 0;
            if (left == IsoDayOfWeek.None || right == IsoDayOfWeek.None)
                return false;
            return TryDaysBetween(DayOfWeekHelper.ToSystemWeek(left), DayOfWeekHelper.ToSystemWeek(right), out days);
        }
    }
}
