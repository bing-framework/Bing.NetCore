using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bing.Utils.Conversions.Internals
{
    /// <summary>
    /// 字符串转short 操作辅助类
    /// </summary>
    internal static class StringShortHelper
    {
        /// <summary>
        /// 是否
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="style">数值格式</param>
        /// <param name="formatProvider">格式化提供程序</param>
        /// <param name="setupAction">操作</param>
        public static bool Is(
            string str,
            NumberStyles style = NumberStyles.Integer,
            IFormatProvider formatProvider = null,
            Action<short> setupAction = null)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;
            if (formatProvider is null)
                formatProvider = NumberFormatInfo.CurrentInfo;
            var result = short.TryParse(str, style, formatProvider, out var number);
            if (result)
                setupAction?.Invoke(number);
            return result;
        }

        /// <summary>
        /// 是否
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="tries">尝试转换集合</param>
        /// <param name="style">数值格式</param>
        /// <param name="formatProvider">格式化提供程序</param>
        /// <param name="setupAction">操作</param>
        public static bool Is(
            string str,
            IEnumerable<IConversionTry<string, short>> tries,
            NumberStyles style = NumberStyles.Integer,
            IFormatProvider formatProvider = null,
            Action<short> setupAction = null)
        {
            if (formatProvider is null)
                formatProvider = NumberFormatInfo.CurrentInfo;
            return Helper.IsXXX(str, string.IsNullOrWhiteSpace, (s, act) => Is(s, style, formatProvider, act), tries,
                setupAction);
        }

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="defaultVal">默认值</param>
        /// <param name="style">数值格式</param>
        /// <param name="formatProvider">格式化提供程序</param>
        public static short To(
            string str,
            short defaultVal = default,
            NumberStyles style = NumberStyles.Integer,
            IFormatProvider formatProvider = null)
        {
            if (formatProvider == null)
                formatProvider = NumberFormatInfo.CurrentInfo;
            return short.TryParse(str, style, formatProvider, out var number) ? number : defaultVal;
        }

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="impls">实现集合</param>
        /// <param name="style">数值格式</param>
        /// <param name="formatProvider">格式化提供程序</param>
        public static short To(
            string str,
            IEnumerable<IConversionImpl<string, short>> impls,
            NumberStyles style = NumberStyles.Integer,
            IFormatProvider formatProvider = null)
        {
            if (formatProvider is null)
                formatProvider = NumberFormatInfo.CurrentInfo;
            return Helper.ToXXX(str, (s, act) => Is(s, style, formatProvider, act), impls);
        }
    }
}
