using System;
using System.Collections.Generic;

namespace Bing.Utils.Conversions.Internals
{
    /// <summary>
    /// 字符串转bool 操作辅助类
    /// </summary>
    internal static class StringBooleanHelper
    {
        /// <summary>
        /// 是否
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="setupAction">操作</param>
        public static bool Is(string str, Action<bool> setupAction = null)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;
            var result = bool.TryParse(str, out var boolean);
            if (result)
                setupAction?.Invoke(boolean);
            return result;
        }

        /// <summary>
        /// 是否
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="tries">尝试转换集合</param>
        /// <param name="setupAction">操作</param>
        public static bool Is(
            string str,
            IEnumerable<IConversionTry<string, bool>> tries,
            Action<bool> setupAction = null) =>
            Helper.IsXXX(str, string.IsNullOrWhiteSpace, Is, tries, setupAction);

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="defaultVal">默认值</param>
        public static bool To(string str, bool defaultVal = default)
        {
            if (string.IsNullOrWhiteSpace(str))
                return defaultVal;
            return bool.TryParse(str, out var boolean) ? boolean : defaultVal;
        }

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="impls">实现集合</param>
        public static bool To(string str, IEnumerable<IConversionImpl<string, bool>> impls) =>
            Helper.ToXXX(str, Is, impls);
    }
}
