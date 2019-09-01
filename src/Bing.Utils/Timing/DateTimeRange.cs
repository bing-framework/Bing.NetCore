using System;

namespace Bing.Utils.Timing
{
    /// <summary>
    /// 时间范围
    /// </summary>
    public interface IDateTimeRange
    {
        /// <summary>
        /// 获取或设置 起始时间
        /// </summary>
        DateTime StartTime { get; set; }

        /// <summary>
        /// 获取或设置 结束时间
        /// </summary>
        DateTime EndTime { get; set; }
    }

    /// <summary>
    /// 时间范围
    /// </summary>
    [Serializable]
    public class DateTimeRange : IDateTimeRange
    {
        /// <summary>
        /// 获取或设置 起始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 获取或设置 结束时间
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 当前时间
        /// </summary>
        private static DateTime Now => DateTime.Now;

        /// <summary>
        /// 周列表
        /// </summary>
        private static DayOfWeek[] Weeks => new[]
        {
            DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday
        };

        #region Yesterday(昨天时间范围)

        /// <summary>
        /// 获取 昨天的时间范围
        /// </summary>
        public static DateTimeRange Yesterday
        {
            get
            {
                var now = Now;
                return new DateTimeRange(now.Date.AddDays(-1), now.Date.AddMilliseconds(-1));
            }
        }

        #endregion

        #region Today(今天时间范围)

        /// <summary>
        /// 获取 今天的时间范围
        /// </summary>
        public static DateTimeRange Today
        {
            get
            {
                var now = Now;
                return new DateTimeRange(now.Date.Date, now.Date.AddDays(1).AddMilliseconds(-1));
            }
        }

        #endregion

        #region Tomorrow(明天时间范围)

        /// <summary>
        /// 获取 明天的时间范围
        /// </summary>
        public static DateTimeRange Tomorrow
        {
            get
            {
                var now = Now;
                return new DateTimeRange(now.Date.AddDays(1), now.Date.AddDays(2).AddMilliseconds(-1));
            }
        }

        #endregion

        #region LastWeek(上周范围)

        /// <summary>
        /// 获取 上周的时间范围
        /// </summary>
        public static DateTimeRange LastWeek
        {
            get
            {
                var now = Now;
                var index = Array.IndexOf(Weeks, now.DayOfWeek);
                return new DateTimeRange(now.Date.AddDays(-index - 7), now.Date.AddDays(-index).AddMilliseconds(-1));
            }
        }

        #endregion

        #region ThisWeek(本周时间范围)

        /// <summary>
        /// 获取 本周的时间范围
        /// </summary>
        public static DateTimeRange ThisWeek
        {
            get
            {
                var now = Now;
                var index = Array.IndexOf(Weeks, now.DayOfWeek);
                return new DateTimeRange(now.Date.AddDays(-index), now.Date.AddDays(7 - index).AddMilliseconds(-1));
            }
        }

        #endregion

        #region NextWeek(下周时间范围)

        /// <summary>
        /// 获取 下周的时间范围
        /// </summary>
        public static DateTimeRange NextWeek
        {
            get
            {
                var now = Now;
                var index = Array.IndexOf(Weeks, now.DayOfWeek);
                return new DateTimeRange(now.Date.AddDays(-index + 7), now.Date.AddDays(14 - index).AddMilliseconds(-1));
            }
        }

        #endregion

        #region LastMonth(上月时间范围)

        /// <summary>
        /// 获取 上个月的时间范围
        /// </summary>
        public static DateTimeRange LastMonth
        {
            get
            {
                var now = Now;
                var startTime = now.Date.AddDays(-now.Day + 1).AddMonths(-1);
                var endTime = startTime.AddMonths(1).AddMilliseconds(-1);
                return new DateTimeRange(startTime, endTime);
            }
        }

        #endregion

        #region ThisMonth(本月时间范围)

        /// <summary>
        /// 获取 本月的时间范围
        /// </summary>
        public static DateTimeRange ThisMonth
        {
            get
            {
                var now = Now;
                var startTime = now.Date.AddDays(-now.Day + 1);
                var endTime = startTime.AddMonths(1).AddMilliseconds(-1);
                return new DateTimeRange(startTime, endTime);
            }
        }

        #endregion

        #region NextMonth(下月时间范围)

        /// <summary>
        /// 获取 下个月的时间范围
        /// </summary>
        public static DateTimeRange NextMonth
        {
            get
            {
                var now = Now;
                var startTime = now.Date.AddDays(-now.Day + 1).AddMonths(1);
                var endTime = startTime.AddMonths(1).AddMilliseconds(-1);
                return new DateTimeRange(startTime, endTime);
            }
        }

        #endregion

        #region LastYear(去年时间范围)

        /// <summary>
        /// 获取 上一年的时间范围
        /// </summary>
        public static DateTimeRange LastYear
        {
            get
            {
                var now = Now;
                return new DateTimeRange(new DateTime(now.Year - 1, 1, 1), new DateTime(now.Year, 1, 1).AddMilliseconds(-1));
            }
        }

        #endregion

        #region ThisYear(今年时间范围)

        /// <summary>
        /// 获取 本年的时间范围
        /// </summary>
        public static DateTimeRange ThisYear
        {
            get
            {
                var now = Now;
                return new DateTimeRange(new DateTime(now.Year, 1, 1), new DateTime(now.Year + 1, 1, 1).AddMilliseconds(-1));
            }
        }

        #endregion

        #region NextYear(明年时间范围)

        /// <summary>
        /// 获取 下一年的时间范围
        /// </summary>
        public static DateTimeRange NextYear
        {
            get
            {
                var now = Now;
                return new DateTimeRange(new DateTime(now.Year + 1, 1, 1), new DateTime(now.Year + 2, 1, 1).AddMilliseconds(-1));
            }
        }

        #endregion

        #region Last7Days(过去7天时间范围)

        /// <summary>
        /// 获取 相对于当前时间过去7天的时间范围
        /// </summary>
        public static DateTimeRange Last7Days
        {
            get
            {
                var now = Now;
                return new DateTimeRange(now.AddDays(-7), now);
            }
        }

        #endregion

        #region Last30Days(过去30天时间范围)

        /// <summary>
        /// 获取 相对于当前时间过去30天的时间范围
        /// </summary>
        public static DateTimeRange Last30Days
        {
            get
            {
                var now = Now;
                return new DateTimeRange(now.AddDays(-30), now);
            }
        }

        #endregion

        #region Last7DaysExceptToday(截止昨天最近7天时间范围)

        /// <summary>
        /// 获取 截止到昨天的最近7天的天数范围
        /// </summary>
        public static DateTimeRange Last7DaysExceptToday
        {
            get
            {
                var now = Now;
                return new DateTimeRange(now.Date.AddDays(-7), now.Date.AddMilliseconds(-1));
            }
        }

        #endregion

        #region Last30DaysExceptToday(截止昨天最近30天时间范围)

        /// <summary>
        /// 获取 截止到昨天的最近30天的天数范围
        /// </summary>
        public static DateTimeRange Last30DaysExceptToday
        {
            get
            {
                var now = Now;
                return new DateTimeRange(now.Date.AddDays(-30), now.Date.AddMilliseconds(-1));
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="DateTimeRange"/>类型的实例
        /// </summary>
        public DateTimeRange() : this(DateTime.MinValue, DateTime.MaxValue)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="DateTimeRange"/>类型的实例
        /// </summary>
        /// <param name="startTime">起始时间</param>
        /// <param name="endTime">结束时间</param>
        public DateTimeRange(DateTime startTime, DateTime endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        /// <summary>
        /// 初始化一个<see cref="DateTimeRange"/>类型的实例
        /// </summary>
        /// <param name="dateTimeRange">事件范围</param>
        public DateTimeRange(IDateTimeRange dateTimeRange) : this(dateTimeRange.StartTime, dateTimeRange.EndTime)
        {
        }

        #endregion

        #region ToString(输出字符串)

        /// <summary>
        /// 输出字符串
        /// </summary>
        public override string ToString() => $"[{StartTime} - {EndTime}]";

        #endregion

        #region GetDays(获取相差天数)

        /// <summary>
        /// 获取两个时间之间的天数
        /// </summary>
        public int GetDays() => Convert.ToInt32(EndTime.Subtract(StartTime).TotalDays);

        #endregion

        #region GetHours(获取相差小时数)

        /// <summary>
        /// 获取两个时间之间的小时数
        /// </summary>
        public int GetHours() => Convert.ToInt32(EndTime.Subtract(StartTime).TotalHours);

        #endregion

        #region GetMinutes(获取相差分钟数)

        /// <summary>
        /// 获取两个时间之间的分钟数
        /// </summary>
        public int GetMinutes() => Convert.ToInt32(EndTime.Subtract(StartTime).TotalMinutes);

        #endregion

        #region GetSeconds(获取相差秒数)

        /// <summary>
        /// 获取两个时间之间的秒数
        /// </summary>
        public int GetSeconds() => Convert.ToInt32(EndTime.Subtract(StartTime).TotalSeconds);

        #endregion

        #region GetMilliseconds(获取相差毫秒数)

        /// <summary>
        /// 获取两个时间之间的毫秒数
        /// </summary>
        public int GetMilliseconds() => Convert.ToInt32(EndTime.Subtract(StartTime).TotalMilliseconds);

        #endregion
    }
}
