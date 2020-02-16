using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bing.Utils.Conversions.Internals
{
    /// <summary>
    /// 字符串转时间跨度 操作辅助类
    /// </summary>
    internal static class StringTimeSpanHelper
    {
        /// <summary>
        /// 是否
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="formatProvider">格式化提供程序</param>
        /// <param name="setupAction">操作</param>
        public static bool Is(
            string str,
            IFormatProvider formatProvider = null,
            Action<TimeSpan> setupAction = null)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;
            if (formatProvider is null)
                formatProvider = DateTimeFormatInfo.CurrentInfo;
            var result = TimeSpan.TryParse(str, formatProvider, out var timeSpan);
            if (result)
                setupAction?.Invoke(timeSpan);
            return result;
        }

        /// <summary>
        /// 是否
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="tries">尝试转换集合</param>
        /// <param name="formatProvider">格式化提供程序</param>
        /// <param name="setupAction">操作</param>
        public static bool Is(
            string str,
            IEnumerable<IConversionTry<string, TimeSpan>> tries,
            IFormatProvider formatProvider = null,
            Action<TimeSpan> setupAction = null) =>
            Helper.IsXXX(str, string.IsNullOrWhiteSpace, (s, act) => Is(s, formatProvider, act), tries,
                setupAction);

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="defaultVal">默认值</param>
        /// <param name="formatProvider">格式化提供程序</param>
        public static TimeSpan To(
            string str,
            TimeSpan defaultVal = default,
            IFormatProvider formatProvider = null)
        {
            if (formatProvider == null)
                formatProvider = DateTimeFormatInfo.CurrentInfo;
            return TimeSpan.TryParse(str, formatProvider, out var timeSpan) ? timeSpan : defaultVal;
        }

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="impls">实现集合</param>
        /// <param name="formatProvider">格式化提供程序</param>
        public static TimeSpan To(
            string str,
            IEnumerable<IConversionImpl<string, TimeSpan>> impls,
            IFormatProvider formatProvider = null)
        {
            if (formatProvider is null)
                formatProvider = DateTimeFormatInfo.CurrentInfo;
            return Helper.ToXXX(str, (s, act) => Is(s, formatProvider, act), impls);
        }
    }
}
