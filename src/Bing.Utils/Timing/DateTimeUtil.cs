using System;
using System.Globalization;

namespace Bing.Utils.Timing
{
    /// <summary>
    /// 时间操作辅助类
    /// </summary>
    public class DateTimeUtil
    {
        #region GetDays(获取总天数)

        /// <summary>
        /// 获取指定年的总天数
        /// </summary>
        /// <param name="year">指定年</param>
        /// <returns></returns>
        public static int GetDays(int year)
        {
            return GetDays(year, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// 获取指定年的总天数，使用指定区域性
        /// </summary>
        /// <param name="year">指定年</param>
        /// <param name="culture">指定区域性</param>
        /// <returns></returns>
        public static int GetDays(int year, CultureInfo culture)
        {
            var first = new DateTime(year, 1, 1, culture.Calendar);
            var last = new DateTime(year + 1, 1, 1, culture.Calendar);
            return GetDays(first, last);
        }

        /// <summary>
        /// 获取指定时间的年的总天数
        /// </summary>
        /// <param name="date">指定时间</param>
        /// <returns></returns>
        public static int GetDays(DateTime date)
        {
            return GetDays(date.Year, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// 获取两个时间之间的天数
        /// </summary>
        /// <param name="fromDate">开始时间</param>
        /// <param name="toDate">结束时间</param>
        /// <returns></returns>
        public static int GetDays(DateTime fromDate, DateTime toDate)
        {            
            return Convert.ToInt32(toDate.Subtract(fromDate).TotalDays);
        }

        #endregion

        #region CalculateAge(计算年龄)

        /// <summary>
        /// 计算年龄
        /// </summary>
        /// <param name="dateOfBirth">出生日期</param>
        /// <returns></returns>
        public static int CalculateAge(DateTime dateOfBirth)
        {
            return CalculateAge(dateOfBirth, DateTime.Now.Date);
        }

        /// <summary>
        /// 计算年龄，指定参考日期
        /// </summary>
        /// <param name="dateOfBirth">出生日期</param>
        /// <param name="referenceDate">参考日期</param>
        /// <returns></returns>
        public static int CalculateAge(DateTime dateOfBirth, DateTime referenceDate)
        {
            var years = referenceDate.Year - dateOfBirth.Year;
            if (referenceDate.Month < dateOfBirth.Month ||
                (referenceDate.Month == dateOfBirth.Month && referenceDate.Day < dateOfBirth.Day))
            {
                --years;
            }
            return years;
        }
        #endregion

        #region BusinessDateFormat(业务时间格式化)
        /// <summary>
        /// 业务时间格式化，返回 xxx前
        /// </summary>
        /// <param name="dateTime">时间</param>
        /// <returns></returns>
        public static string BusinessDateFormat(DateTime dateTime)
        {
            TimeSpan span = (DateTime.Now - dateTime).Duration();
            if (span.TotalDays > 60)
            {
                return dateTime.ToString("yyyy-MM-dd");
            }
            if (span.TotalDays > 30)
            {
                return "1个月前";
            }
            if (span.TotalDays > 14)
            {
                return "2周前";
            }
            if (span.TotalDays > 7)
            {
                return "1周前";
            }
            if (span.TotalDays > 1)
            {
                return $"{(int)Math.Floor(span.TotalDays)}天前";
            }
            if (span.TotalHours > 1)
            {
                return $"{(int)Math.Floor(span.TotalHours)}小时前";
            }
            if (span.TotalMinutes > 1)
            {
                return $"{(int)Math.Floor(span.TotalMinutes)}秒前";
            }
            return "1秒前";
        }

        #endregion
    }
}
