using System;
using System.Collections.Generic;

namespace Bing.Utils.Conversions.Internals
{
    /// <summary>
    /// 字符串转版本 操作辅助类
    /// </summary>
    internal static class StringVersionHelper
    {
        /// <summary>
        /// 是否
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="setupAction">操作</param>
        public static bool Is(
            string str,
            Action<Version> setupAction = null)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;
            var result = Version.TryParse(str, out var c);
            if (result)
                setupAction?.Invoke(c);
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
            IEnumerable<IConversionTry<string, Version>> tries,
            Action<Version> setupAction = null) =>
            Helper.IsXXX(str, string.IsNullOrWhiteSpace, Is, tries, setupAction);

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="defaultVal">默认值</param>
        public static Version To(
            string str,
            Version defaultVal = default) =>
            Version.TryParse(str, out var c) ? c : defaultVal;

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="impls">实现集合</param>
        public static Version To(string str, IEnumerable<IConversionImpl<string, Version>> impls) => Helper.ToXXX(str, Is, impls);
    }
}
