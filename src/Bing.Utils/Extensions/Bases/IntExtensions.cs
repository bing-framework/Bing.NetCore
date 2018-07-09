using System;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 整型(<see cref="Int32"/>) 扩展
    /// </summary>
    public static class IntExtensions
    {
        #region Times(执行n次指定操作)

        /// <summary>
        /// 执行n次指定操作，基于底层int值
        /// </summary>
        /// <param name="value">int</param>
        /// <param name="action">操作</param>
        public static void Times(this int value, Action action)
        {
            value.AsLong().Times(action);
        }

        /// <summary>
        /// 执行n次指定操作，基于底层int值
        /// </summary>
        /// <param name="value">int</param>
        /// <param name="action">操作</param>
        public static void Times(this int value, Action<int> action)
        {
            for (var i = 0; i < value; i++)
            {
                action(i);
            }
        }

        #endregion

        #region IsEven(是否偶数)

        /// <summary>
        /// 是否偶数
        /// </summary>
        /// <param name="value">int</param>
        /// <returns>bool</returns>
        public static bool IsEven(this int value)
        {
            return value.AsLong().IsEven();
        }

        #endregion

        #region IsOdd(是否奇数)

        /// <summary>
        /// 是否奇数
        /// </summary>
        /// <param name="value">int</param>
        /// <returns>bool</returns>
        public static bool IsOdd(this int value)
        {
            return value.AsLong().IsOdd();
        }

        #endregion

        #region InRange(判断值是否在指定范围内)

        /// <summary>
        /// 判断当前值是否在指定范围内
        /// </summary>
        /// <param name="value">int</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        /// <returns>bool</returns>
        public static bool InRange(this int value, int minValue, int maxValue)
        {
            return value.AsLong().InRange(minValue, maxValue);
        }

        /// <summary>
        /// 判断值是否在指定范围内，否则返回默认值
        /// </summary>
        /// <param name="value">long</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>int</returns>
        public static int InRange(this int value, int minValue, int maxValue, int defaultValue)
        {
            return (int)value.AsLong().InRange(minValue, maxValue, defaultValue);
        }

        #endregion

        #region IsPrime(是否质数)

        /// <summary>
        /// 是否质数（素数），一个质数（或素数）是具有两个不同约束的自然数：1和它本身
        /// </summary>
        /// <param name="value">int</param>
        /// <returns>bool</returns>
        public static bool IsPrime(this int value)
        {
            return value.AsLong().IsPrime();
        }

        #endregion

        #region ToOrdinal(数值转换为顺序序号)

        /// <summary>
        /// 将数值转换为顺序序号，（英语序号）
        /// </summary>
        /// <param name="i">int</param>
        /// <returns>返回的字符串包含序号标记毗邻的数字表示</returns>
        public static string ToOrdinal(this int i)
        {
            return i.AsLong().ToOrdinal();
        }

        /// <summary>
        /// 将数值转换为指定格式的序号字符串，（英语序号）
        /// </summary>
        /// <param name="i">int</param>
        /// <param name="format">自定义格式</param>
        /// <returns>返回的字符串包含序号标记毗邻的数字表示</returns>
        public static string ToOrdinal(this int i, string format)
        {
            return i.AsLong().ToOrdinal(format);
        }

        #endregion

        #region AsLong(Int转为Long类型)

        /// <summary>
        /// Int转为Long类型
        /// </summary>
        /// <param name="i">int</param>
        /// <returns>long</returns>
        public static long AsLong(this int i)
        {
            return i;
        }

        #endregion

        #region IsIndexInArray(判断索引是否在数组范围内)

        /// <summary>
        /// 判断索引是否在数组指定范围内
        /// </summary>
        /// <param name="index">索引</param>
        /// <param name="array">数组</param>
        /// <returns>bool</returns>
        public static bool IsIndexInArray(this int index, Array array)
        {
            return index.GetArrayIndex().InRange(array.GetLowerBound(0), array.GetUpperBound(0));
        }

        #endregion

        #region GetArrayIndex(获取真实数组索引)

        /// <summary>
        /// 获取真实数组索引
        /// </summary>
        /// <param name="at">int</param>
        /// <returns>数组索引</returns>
        public static int GetArrayIndex(this int at)
        {
            return at == 0 ? 0 : at - 1;
        }

        #endregion

        #region Days(获取日期间隔)

        /// <summary>
        /// 获取日期间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="days">int</param>
        /// <returns>日期间隔</returns>
        public static TimeSpan Days(this int days)
        {
            return TimeSpan.FromDays(days);
        }

        #endregion

        #region Hours(获取小时间隔)

        /// <summary>
        /// 获取小时间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="hours">int</param>
        /// <returns>小时间隔</returns>
        public static TimeSpan Hours(this int hours)
        {
            return TimeSpan.FromHours(hours);
        }

        #endregion

        #region Minutes(获取分钟间隔)

        /// <summary>
        /// 获取分钟间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="minutes">int</param>
        /// <returns>分钟间隔</returns>
        public static TimeSpan Minutes(this int minutes)
        {
            return TimeSpan.FromMinutes(minutes);
        }

        #endregion

        #region Seconds(获取秒间隔)

        /// <summary>
        /// 获取秒间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="seconds">int</param>
        /// <returns>秒间隔</returns>
        public static TimeSpan Seconds(this int seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        #endregion

        #region Milliseconds(获取毫秒间隔)

        /// <summary>
        /// 获取毫秒间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="milliseconds">int</param>
        /// <returns>毫秒间隔</returns>
        public static TimeSpan Milliseconds(this int milliseconds)
        {
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        #endregion

        #region Ticks(获取刻度间隔)

        /// <summary>
        /// 获取刻度间隔，根据数值获取时间间隔
        /// </summary>
        /// <param name="ticks">int</param>
        /// <returns>刻度间隔</returns>
        public static TimeSpan Ticks(this int ticks)
        {
            return TimeSpan.FromTicks(ticks);
        }

        #endregion
    }
}
