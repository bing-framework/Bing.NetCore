using System;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 处理日期的类
    /// </summary>
    public class DateHelper
    {
        /// <summary>
        /// 根据当前日期确定当前是星期几
        /// </summary>
        /// <param name="strDate">The string date.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception"></exception>
        public string GetWeekDay(string strDate)
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
        public string GetWeekDay(DateTime dTime)
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
        /// 获取当前年的最大周数
        /// </summary>
        /// <param name="year">The year.</param>
        /// <returns>System.Int32.</returns>
        /// <exception cref="System.Exception"></exception>
        public int GetMaxWeekOfYear(int year)
        {
            try
            {
                DateTime tempDate = new DateTime(year, 12, 31);
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
        public int GetMaxWeekOfYear(DateTime dTime)
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

        /// <summary>
        /// 根据时间获取当前是第几周
        /// </summary>
        /// <param name="dTime">The d time.</param>
        /// <returns>System.Int32.</returns>
        /// <exception cref="System.Exception"></exception>
        public int GetWeekIndex(DateTime dTime)
        {
            //如果12月31号与下一年的1月1好在同一个星期则算下一年的第一周
            try
            {
                //需要判断的时间
                //DateTime dTime = Convert.ToDateTime(strDate);
                //确定此时间在一年中的位置
                int dayOfYear = dTime.DayOfYear;

                //DateTime tempDate = new DateTime(dTime.Year,1,6,calendar);
                //当年第一天
                DateTime tempDate = new DateTime(dTime.Year, 1, 1);

                //确定当年第一天
                int tempDayOfWeek = (int)tempDate.DayOfWeek;
                tempDayOfWeek = tempDayOfWeek == 0 ? 7 : tempDayOfWeek;
                //确定星期几
                int index = (int)dTime.DayOfWeek;

                index = index == 0 ? 7 : index;

                //当前周的范围
                DateTime retStartDay = dTime.AddDays(-(index - 1));
                DateTime retEndDay = dTime.AddDays(7 - index);

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
        public int GetWeekIndex(string strDate)
        {
            try
            {
                //需要判断的时间
                DateTime dTime = Convert.ToDateTime(strDate);
                return GetWeekIndex(dTime);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 根据时间取周的日期范围
        /// </summary>
        /// <param name="strDate">The string date.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception"></exception>
        public string GetWeekRange(string strDate)
        {
            try
            {
                //需要判断的时间
                DateTime dTime = Convert.ToDateTime(strDate);
                return GetWeekRange(dTime);
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
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception"></exception>
        public string GetWeekRange(DateTime dTime)
        {
            try
            {
                int index = (int)dTime.DayOfWeek;

                index = index == 0 ? 7 : index;

                //当前周的范围
                DateTime retStartDay = dTime.AddDays(-(index - 1));
                DateTime retEndDay = dTime.AddDays(7 - index);

                return WeekRangeToString(retStartDay, retEndDay);
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
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception">
        /// 请输入大于0的整数
        /// or
        /// 今年没有第 + weekIndex + 周。
        /// or
        /// </exception>
        public string GetWeekRange(int year, int weekIndex)
        {
            try
            {
                if (weekIndex < 1)
                {
                    throw new Exception("请输入大于0的整数");
                }

                int allDays = (weekIndex - 1) * 7;
                //确定当年第一天
                DateTime firstDate = new DateTime(year, 1, 1);
                int firstDayOfWeek = (int)firstDate.DayOfWeek;

                firstDayOfWeek = firstDayOfWeek == 0 ? 7 : firstDayOfWeek;

                //周开始日
                int startAddDays = allDays + (1 - firstDayOfWeek);
                DateTime weekRangeStart = firstDate.AddDays(startAddDays);
                //周结束日
                int endAddDays = allDays + (7 - firstDayOfWeek);
                DateTime weekRangeEnd = firstDate.AddDays(endAddDays);

                if (weekRangeStart.Year > year ||
                 (weekRangeStart.Year == year && weekRangeEnd.Year > year))
                {
                    throw new Exception("今年没有第" + weekIndex + "周。");
                }

                return WeekRangeToString(weekRangeStart, weekRangeEnd);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 根据时间取周的日期范围
        /// </summary>
        /// <param name="weekIndex">Index of the week.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.Exception"></exception>
        public string GetWeekRange(int weekIndex)
        {
            try
            {
                return GetWeekRange(DateTime.Now.Year, weekIndex);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Weeks the range to string.
        /// </summary>
        /// <param name="weekRangeStart">The week range start.</param>
        /// <param name="weekRangeEnd">The week range end.</param>
        /// <returns>System.String.</returns>
        private string WeekRangeToString(DateTime weekRangeStart, DateTime weekRangeEnd)
        {
            string strWeekRangeStart = weekRangeStart.ToString("yyyy/MM/dd");
            string strWeekRangeend = weekRangeEnd.ToString("yyyy/MM/dd");

            return strWeekRangeStart + "～" + strWeekRangeend;
        }

        /// <summary>
        /// 转换星期的表示方法
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>System.String.</returns>
        public string GetWeekDay(int index)
        {
            string retVal = string.Empty;
            switch (index)
            {
                case 0:
                    {
                        retVal = "星期日";
                        break;
                    }
                case 1:
                    {
                        retVal = "星期一";
                        break;
                    }
                case 2:
                    {
                        retVal = "星期二";
                        break;
                    }
                case 3:
                    {
                        retVal = "星期三";
                        break;
                    }
                case 4:
                    {
                        retVal = "星期四";
                        break;
                    }
                case 5:
                    {
                        retVal = "星期五";
                        break;
                    }
                case 6:
                    {
                        retVal = "星期六";
                        break;
                    }
            }

            return retVal;
        }

        /// <summary>
        /// 计算第几周（重新修改后）
        /// </summary>
        /// <param name="dt">The dt.</param>
        /// <returns>System.Int32.</returns>
        public int GetWeekOfCurrDate(DateTime dt)
        {
            int Week = 1;

            int nYear = dt.Year;

            System.DateTime FirstDayInYear = new DateTime(nYear, 1, 1);

            System.DateTime LastDayInYear = new DateTime(nYear, 12, 31);

            int DaysOfYear = Convert.ToInt32(LastDayInYear.DayOfYear);

            int WeekNow = Convert.ToInt32(FirstDayInYear.DayOfWeek) - 1;

            if (WeekNow < 0) WeekNow = 6;

            int DayAdd = 6 - WeekNow;

            System.DateTime BeginDayOfWeek = new DateTime(nYear, 1, 1);

            System.DateTime EndDayOfWeek = BeginDayOfWeek.AddDays(DayAdd);

            Week = 2;

            for (int i = DayAdd + 1; i <= DaysOfYear; i++)
            {
                BeginDayOfWeek = FirstDayInYear.AddDays(i);

                if (i + 6 > DaysOfYear)
                {
                    EndDayOfWeek = BeginDayOfWeek.AddDays(DaysOfYear - i - 1);
                }
                else
                {
                    EndDayOfWeek = BeginDayOfWeek.AddDays(6);
                }

                if (dt.Month == EndDayOfWeek.Month && dt.Day <= EndDayOfWeek.Day)
                {
                    break;
                }

                Week++;

                i = i + 6;
            }

            return Week;
        }

        /// <summary>
        /// 获取时间字符串(刚刚、分钟前、小时前、天前、yyyy-MM-dd HH:mm:ss)
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static string GetDateTimeString(DateTime dt)
        {
            return GetDateTimeString(dt, "yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 获取时间字符串(刚刚、分钟前、小时前、天前)
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="defaultFormat"></param>
        /// <returns></returns>
        public static string GetDateTimeString(DateTime dt, string defaultFormat)
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

        /// <summary>
        /// 获取时间范围
        /// </summary>
        /// <param name="range"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public static void GetDateRange(DateRangeEnum range, out DateTime startDate, out DateTime endDate)
        {
            GetDateRange(DateTime.Now, range, out startDate, out endDate);
        }

        /// <summary>
        /// 获取时间范围
        /// </summary>
        /// <param name="date"></param>
        /// <param name="range"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
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

        /// <summary>
        /// 日期范围
        /// </summary>
        public enum DateRangeEnum
        {
            /// <summary>
            ///
            /// </summary>
            Week,

            /// <summary>
            ///
            /// </summary>
            Month,

            /// <summary>
            ///
            /// </summary>
            Quarter,

            /// <summary>
            ///
            /// </summary>
            HalfYear,

            /// <summary>
            ///
            /// </summary>
            Year
        }
    }
}
