using System;
using System.Globalization;

namespace Bing.Utils.Timing
{
    /// <summary>
    /// 时间操作辅助类
    /// </summary>
    public class DateTimeHelper
    {
        #region GetDays(获取总天数)

        /// <summary>
        /// 获取指定年的总天数
        /// </summary>
        /// <param name="year">指定年</param>
        public static int GetDays(int year) => GetDays(year, CultureInfo.CurrentCulture);

        /// <summary>
        /// 获取指定年的总天数，使用指定区域性
        /// </summary>
        /// <param name="year">指定年</param>
        /// <param name="culture">指定区域性</param>
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
        public static int GetDays(DateTime date) => GetDays(date.Year, CultureInfo.CurrentCulture);

        /// <summary>
        /// 获取两个时间之间的天数
        /// </summary>
        /// <param name="fromDate">开始时间</param>
        /// <param name="toDate">结束时间</param>
        public static int GetDays(DateTime fromDate, DateTime toDate) => Convert.ToInt32(toDate.Subtract(fromDate).TotalDays);

        #endregion

        #region CalculateAge(计算年龄)

        /// <summary>
        /// 计算年龄
        /// </summary>
        /// <param name="dateOfBirth">出生日期</param>
        public static int CalculateAge(DateTime dateOfBirth) => CalculateAge(dateOfBirth, DateTime.Now.Date);

        /// <summary>
        /// 计算年龄，指定参考日期
        /// </summary>
        /// <param name="dateOfBirth">出生日期</param>
        /// <param name="referenceDate">参考日期</param>
        public static int CalculateAge(DateTime dateOfBirth, DateTime referenceDate)
        {
            var years = referenceDate.Year - dateOfBirth.Year;
            if (referenceDate.Month < dateOfBirth.Month ||
                (referenceDate.Month == dateOfBirth.Month && referenceDate.Day < dateOfBirth.Day))
                --years;
            return years;
        }

        #endregion

        #region BusinessDateFormat(业务时间格式化)

        /// <summary>
        /// 业务时间格式化，返回:大于60天-"yyyy-MM-dd",31~60天-1个月前，15~30天-2周前,8~14天-1周前,1~7天-x天前 ,大于1小时-x小时前,x秒前
        /// </summary>
        /// <param name="dateTime">时间</param>
        public static string BusinessDateFormat(DateTime dateTime)
        {
            var span = (DateTime.Now - dateTime).Duration();
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

        /// <summary>
        /// 获取时间字符串(小于5分-刚刚、5~60分-x分钟前、1~24小时-x小时前、1~60天-x天前、yyyy-MM-dd HH:mm:ss)
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="defaultFormat"></param>
        public static string BusinessDateFormat(DateTime dt, string defaultFormat = "yyyy-MM-dd HH:mm:ss")
        {
            var timeSpan = DateTime.Now - dt;
            string result = string.Empty;

            if (timeSpan.TotalMinutes < 5)
                result = string.Format("刚刚");
            else if (timeSpan.TotalMinutes < 60)
                result = string.Format("{0}分钟前", (int)timeSpan.TotalMinutes);
            else if (timeSpan.TotalMinutes < 60 * 24)
                result = string.Format("{0}小时前", (int)timeSpan.TotalHours);
            else if (timeSpan.TotalMinutes <= 60 * 24 * 7)
                result = string.Format("{0}天前", (int)timeSpan.TotalDays);
            else
                result = dt.ToString(defaultFormat);

            return result;
        }

        #endregion

        #region GetWeekDay(计算当前为星期几)

        /// <summary>
        /// 根据当前日期确定当前是星期几
        /// </summary>
        /// <param name="strDate">The string date.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception"></exception>
        public static DayOfWeek GetWeekDay(string strDate)
        {
            try
            {
                //需要判断的时间
                DateTime dTime = Convert.ToDateTime(strDate);
                return GetWeekDay(dTime);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 根据当前日期确定当前是星期几
        /// </summary>
        /// <param name="dTime">The d time.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception"></exception>
        public static DayOfWeek GetWeekDay(DateTime dTime)
        {
            try
            {
                //确定星期几
                int index = (int)dTime.DayOfWeek;
                return GetWeekDay(index);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 转换星期的表示方法
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>System.String.</returns>
        private static DayOfWeek GetWeekDay(int index)
        {
            return (DayOfWeek)index;
        }

        /// <summary>
        /// 转换星期的表示方法
        /// </summary>
        /// <param name="dayOfWeek">The index.</param>
        /// <returns>System.String.</returns>
        public static string GetChineseWeekDay(DayOfWeek dayOfWeek)
        {
            string retVal = string.Empty;

            switch (dayOfWeek)
            {
                case DayOfWeek.Sunday:
                    retVal = "星期日";
                    break;

                case DayOfWeek.Monday:
                    retVal = "星期一";
                    break;

                case DayOfWeek.Tuesday:
                    retVal = "星期二";
                    break;

                case DayOfWeek.Wednesday:
                    retVal = "星期三";
                    break;

                case DayOfWeek.Thursday:
                    retVal = "星期四";
                    break;

                case DayOfWeek.Friday:
                    retVal = "星期五";
                    break;

                case DayOfWeek.Saturday:
                    retVal = "星期六";
                    break;

                default:
                    break;
            }

            return retVal;
        }

        #endregion

        #region GetMaxWeekOfYear(计算当前年的最大周数)

        /// <summary>
        /// 获取当前年的最大周数
        /// </summary>
        /// <param name="year">The year.</param>
        /// <returns>System.Int32.</returns>
        /// <exception cref="System.Exception"></exception>
        public static int GetMaxWeekOfYear(int year)
        {
            try
            {
                var tempDate = new DateTime(year, 12, 31);
                int tempDayOfWeek = (int)tempDate.DayOfWeek;
                if (tempDayOfWeek != 0)
                {
                    tempDate = tempDate.Date.AddDays(-tempDayOfWeek);
                }
                return GetWeekIndex(tempDate);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 获取当前年的最大周数
        /// </summary>
        /// <param name="dTime">The d time.</param>
        /// <returns>System.Int32.</returns>
        /// <exception cref="System.Exception"></exception>
        public static int GetMaxWeekOfYear(DateTime dTime)
        {
            try
            {
                return GetMaxWeekOfYear(dTime.Year);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        #endregion

        #region GetWeekIndex(计算当前是第几周)

        /// <summary>
        /// 根据时间获取当前是第几周
        /// </summary>
        /// <param name="dTime">The d time.</param>
        /// <returns>System.Int32.</returns>
        /// <exception cref="System.Exception"></exception>
        public static int GetWeekIndex(DateTime dTime)
        {
            //如果12月31号与下一年的1月1好在同一个星期则算下一年的第一周
            try
            {
                //确定此时间在一年中的位置
                int dayOfYear = dTime.DayOfYear;

                //当年第一天
                var tempDate = new DateTime(dTime.Year, 1, 1);

                //确定当年第一天
                int tempDayOfWeek = (int)tempDate.DayOfWeek;
                tempDayOfWeek = tempDayOfWeek == 0 ? 7 : tempDayOfWeek;

                //确定星期几
                int index = (int)dTime.DayOfWeek;
                index = index == 0 ? 7 : index;

                //当前周的范围
                var retStartDay = dTime.AddDays(-(index - 1));
                var retEndDay = dTime.AddDays(7 - index);

                //确定当前是第几周
                int weekIndex = (int)Math.Ceiling(((double)dayOfYear + tempDayOfWeek - 1) / 7);

                if (retStartDay.Year < retEndDay.Year)
                {
                    weekIndex = 1;
                }

                return weekIndex;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 根据时间获取当前是第几周
        /// </summary>
        /// <param name="strDate">The string date.</param>
        /// <returns>System.Int32.</returns>
        /// <exception cref="System.Exception"></exception>
        public static int GetWeekIndex(string strDate)
        {
            try
            {
                //需要判断的时间
                var dTime = Convert.ToDateTime(strDate);
                return GetWeekIndex(dTime);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        #endregion

        #region GetWeekRange(计算周范围)

        /// <summary>
        /// 根据时间取周的日期范围
        /// </summary>
        /// <param name="strDate">The string date.</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception"></exception>
        public static void GetWeekRange(string strDate, out DateTime startDate, out DateTime endDate)
        {
            try
            {
                //需要判断的时间
                var dTime = Convert.ToDateTime(strDate);
                GetWeekRange(dTime, out startDate, out endDate);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 根据时间取周的日期范围
        /// </summary>
        /// <param name="dTime">The d time.</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception"></exception>
        public static void GetWeekRange(DateTime dTime, out DateTime startDate, out DateTime endDate)
        {
            try
            {
                int index = (int)dTime.DayOfWeek;
                index = index == 0 ? 7 : index;

                startDate = dTime.AddDays(-(index - 1));
                endDate = dTime.AddDays(7 - index);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 根据时间取周的日期范围
        /// </summary>
        /// <param name="year">The year.</param>
        /// <param name="weekIndex">Index of the week.</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception">
        /// 请输入大于0的整数
        /// or
        /// 今年没有第 + weekIndex + 周。
        /// or
        /// </exception>
        public static void GetWeekRange(int year, int weekIndex, out DateTime startDate, out DateTime endDate)
        {
            if (weekIndex < 1)
            {
                throw new Exception("请输入大于0的整数");
            }

            int allDays = (weekIndex - 1) * 7;

            //确定当年第一天
            var firstDate = new DateTime(year, 1, 1);
            int firstDayOfWeek = (int)firstDate.DayOfWeek;
            firstDayOfWeek = firstDayOfWeek == 0 ? 7 : firstDayOfWeek;

            //周开始日
            int startAddDays = allDays + (1 - firstDayOfWeek);
            var weekRangeStart = firstDate.AddDays(startAddDays);

            //周结束日
            int endAddDays = allDays + (7 - firstDayOfWeek);
            var weekRangeEnd = firstDate.AddDays(endAddDays);

            if (weekRangeStart.Year > year ||
             (weekRangeStart.Year == year && weekRangeEnd.Year > year))
            {
                throw new Exception("今年没有第" + weekIndex + "周。");
            }

            startDate = weekRangeStart;
            endDate = weekRangeEnd;
        }

        /// <summary>
        /// 根据时间取周的日期范围
        /// </summary>
        /// <param name="weekIndex">Index of the week.</param>
        /// <param name="startDate">输出开始日期</param>
        /// <param name="endDate">输出结束日期</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception"></exception>
        public static void GetWeekRange(int weekIndex, out DateTime startDate, out DateTime endDate)
        {
            try
            {
                GetWeekRange(DateTime.Now.Year, weekIndex, out startDate, out endDate);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        #endregion

        #region GetDateRange(计算当前时间范围)

        /// <summary>
        /// 获取当前的时间范围
        /// </summary>
        /// <param name="range"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public static void GetDateRange(DateRangeEnum range, out DateTime startDate, out DateTime endDate)
        {
            GetDateRange(DateTime.Now, range, out startDate, out endDate);
        }

        /// <summary>
        /// 获取当前时间范围
        /// </summary>
        /// <param name="date">当前日期</param>
        /// <param name="range">日期范围</param>
        /// <param name="startDate">输出开始日期</param>
        /// <param name="endDate">输出结束日期</param>
        public static void GetDateRange(DateTime date, DateRangeEnum range, out DateTime startDate, out DateTime endDate)
        {
            switch (range)
            {
                case DateRangeEnum.Week:

                    startDate = date.AddDays(-(int)date.DayOfWeek).Date;
                    endDate = date.AddDays(6 - (int)date.DayOfWeek + 1).Date.AddSeconds(-1);
                    break;

                case DateRangeEnum.Month:
                    startDate = new DateTime(date.Year, date.Month, 1);
                    endDate = startDate.AddMonths(1).Date.AddSeconds(-1);
                    break;

                case DateRangeEnum.Quarter:
                    if (date.Month <= 3)
                    {
                        startDate = new DateTime(date.Year, 1, 1);
                    }
                    else if (date.Month <= 6)
                    {
                        startDate = new DateTime(date.Year, 4, 1);
                    }
                    else if (date.Month <= 9)
                    {
                        startDate = new DateTime(date.Year, 7, 1);
                    }
                    else
                    {
                        startDate = new DateTime(date.Year, 10, 1);
                    }
                    endDate = startDate.AddMonths(3).AddSeconds(-1);
                    break;

                case DateRangeEnum.HalfYear:
                    if (date.Month <= 6)
                    {
                        startDate = new DateTime(date.Year, 1, 1);
                    }
                    else
                    {
                        startDate = new DateTime(date.Year, 7, 1);
                    }
                    endDate = startDate.AddMonths(6).AddSeconds(-1);
                    break;

                case DateRangeEnum.Year:
                    startDate = new DateTime(date.Year, 1, 1);
                    endDate = startDate.AddYears(1).AddSeconds(-1);
                    break;

                default:
                    startDate = DateTime.MinValue;
                    endDate = DateTime.MinValue;
                    break;
            }
        }

        #endregion
    }
}
