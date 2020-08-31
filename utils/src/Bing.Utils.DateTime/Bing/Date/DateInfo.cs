using System;
using Bing.Extensions;
using NodaTime;

namespace Bing.Date
{
    /// <summary>
    /// 日期信息
    /// </summary>
    public class DateInfo
    {
        /// <summary>
        /// 内部日期时间
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private DateTime _internalDateTime { get; set; }

        /// <summary>
        /// 初始化一个<see cref="DateInfo"/>类型的实例
        /// </summary>
        internal DateInfo() { }

        /// <summary>
        /// 初始化一个<see cref="DateInfo"/>类型的实例
        /// </summary>
        /// <param name="dt">日期时间</param>
        public DateInfo(DateTime dt) => _internalDateTime = dt.SetTime(0, 0, 0, 0);

        /// <summary>
        /// 初始化一个<see cref="DateInfo"/>类型的实例
        /// </summary>
        /// <param name="year">年</param>
        /// <param name="month">月</param>
        /// <param name="day">日</param>
        public DateInfo(int year, int month, int day) => _internalDateTime = new DateTime(year, month, day, 0, 0, 0, 0);

        /// <summary>
        /// 年
        /// </summary>
        public virtual int Year
        {
            get => _internalDateTime.Year;
            set => _internalDateTime = _internalDateTime.SetYear(value);
        }

        /// <summary>
        /// 月
        /// </summary>
        public virtual int Month
        {
            get => _internalDateTime.Month;
            set => _internalDateTime = _internalDateTime.SetMonth(value);
        }

        /// <summary>
        /// 日
        /// </summary>
        public virtual int Day
        {
            get => _internalDateTime.Day;
            set => _internalDateTime = _internalDateTime.SetDay(value);
        }

        /// <summary>
        /// 内部日期时间引用
        /// </summary>
        internal DateTime DateTimeRef => _internalDateTime;

        /// <summary>
        /// 日期检查
        /// </summary>
        private static class DateChecker
        {
            /// <summary>
            /// 检查月份
            /// </summary>
            /// <param name="monthValue">月份值</param>
            public static int CheckMonth(int monthValue)
            {
                if (monthValue < 0 || monthValue > 12)
                    throw new ArgumentOutOfRangeException(nameof(monthValue), monthValue, "月份应该在1-12范围内");
                return monthValue;
            }
        }

        /// <summary>
        /// 转换为 <see cref="DateTime"/>
        /// </summary>
        public virtual DateTime ToDateTime() => _internalDateTime.Clone();

        /// <summary>
        /// 转换为 <see cref="LocalDate"/>
        /// </summary>
        public LocalDate ToLocalDate() => LocalDate.FromDateTime(_internalDateTime);

        /// <summary>
        /// 转换为 <see cref="LocalDateTime"/>
        /// </summary>
        public LocalDateTime ToLocalDateTime() => LocalDateTime.FromDateTime(_internalDateTime);

        /// <summary>
        /// 添加天数
        /// </summary>
        /// <param name="days">天数</param>
        public DateInfo AddDays(int days)
        {
            _internalDateTime += days.Days();
            return this;
        }

    }
}
