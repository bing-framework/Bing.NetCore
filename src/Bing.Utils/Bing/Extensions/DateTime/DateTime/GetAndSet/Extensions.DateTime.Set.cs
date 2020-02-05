using System;
using Bing.Utils.Date;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 日期时间(<see cref="DateTime"/>) 扩展方法
    /// </summary>
    public static partial class DateTimeExtensions
    {
        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="hour">时</param>
        public static DateTime SetTime(this DateTime dt, int hour) => DateTimeFactory.Create(dt.Year, dt.Month, dt.Day, hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="hour">时</param>
        /// <param name="minute">分</param>
        public static DateTime SetTime(this DateTime dt, int hour, int minute) => DateTimeFactory.Create(dt.Year, dt.Month, dt.Day, hour, minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="hour">时</param>
        /// <param name="minute">分</param>
        /// <param name="second">秒</param>
        public static DateTime SetTime(this DateTime dt, int hour, int minute, int second) => DateTimeFactory.Create(dt.Year, dt.Month, dt.Day, hour, minute, second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="hour">时</param>
        /// <param name="minute">分</param>
        /// <param name="second">秒</param>
        /// <param name="millisecond">毫秒</param>
        public static DateTime SetTime(this DateTime dt, int hour, int minute, int second, int millisecond) => DateTimeFactory.Create(dt.Year, dt.Month, dt.Day, hour, minute, second, millisecond, dt.Kind);

        /// <summary>
        /// 设置时间 - 小时
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="hour">时</param>
        public static DateTime SetHour(this DateTime dt, int hour) => DateTimeFactory.Create(dt.Year, dt.Month, dt.Day, hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置时间 - 分钟
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="minute">分</param>
        public static DateTime SetMinute(this DateTime dt, int minute) => DateTimeFactory.Create(dt.Year, dt.Month, dt.Day, dt.Hour, minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置时间 - 秒
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="second">秒</param>
        public static DateTime SetSecond(this DateTime dt, int second) => DateTimeFactory.Create(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置时间 - 毫秒
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="millisecond">毫秒</param>
        public static DateTime SetMillisecond(this DateTime dt, int millisecond) => DateTimeFactory.Create(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, millisecond, dt.Kind);

        /// <summary>
        /// 设置时间为凌晨0点
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime Midnight(this DateTime dt) => throw new NotImplementedException();

        /// <summary>
        /// 设置时间为中午12点
        /// </summary>
        /// <param name="dt">时间</param>
        public static DateTime Noon(this DateTime dt) => dt.SetTime(12, 0, 0, 0);

        /// <summary>
        /// 设置日期
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="year">年</param>
        public static DateTime SetDate(this DateTime dt, int year)=> DateTimeFactory.Create(year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置日期
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="year">年</param>
        /// <param name="month">月</param>
        public static DateTime SetDate(this DateTime dt, int year, int month) => DateTimeFactory.Create(year, month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置日期
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="year">年</param>
        /// <param name="month">月</param>
        /// <param name="day">日</param>
        public static DateTime SetDate(this DateTime dt, int year, int month, int day) => DateTimeFactory.Create(year, month, day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置日期 - 年
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="year">年</param>
        public static DateTime SetYear(this DateTime dt, int year) => DateTimeFactory.Create(year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置日期 - 月
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="month">月</param>
        public static DateTime SetMonth(this DateTime dt, int month) => DateTimeFactory.Create(dt.Year, month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置日期 - 日
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="day">日</param>
        public static DateTime SetDay(this DateTime dt, int day) => DateTimeFactory.Create(dt.Year, dt.Month, day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

        /// <summary>
        /// 设置日期种类。本地/UTC
        /// </summary>
        /// <param name="dt">时间</param>
        /// <param name="kind">日期种类</param>
        public static DateTime SetKind(this DateTime dt, DateTimeKind kind) => DateTimeFactory.Create(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, kind);
    }
}
