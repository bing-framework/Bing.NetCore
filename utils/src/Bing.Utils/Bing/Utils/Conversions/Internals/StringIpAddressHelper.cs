using System;
using System.Collections.Generic;
using System.Net;

namespace Bing.Utils.Conversions.Internals
{
    /// <summary>
    /// 字符串转IP地址 操作辅助类
    /// </summary>
    internal static class StringIpAddressHelper
    {
        /// <summary>
        /// 是否
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="setupAction">操作</param>
        public static bool Is(
            string str,
            Action<IPAddress> setupAction = null)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;
            var result = IPAddress.TryParse(str, out var address);
            if (result)
                setupAction?.Invoke(address);
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
            IEnumerable<IConversionTry<string, IPAddress>> tries,
            Action<IPAddress> setupAction = null) =>
            Helper.IsXXX(str, string.IsNullOrWhiteSpace, Is, tries, setupAction);

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="defaultVal">默认值</param>
        public static IPAddress To(
            string str,
            IPAddress defaultVal = default) =>
            IPAddress.TryParse(str, out var address) ? address : defaultVal;

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="impls">实现集合</param>
        public static IPAddress To(string str, IEnumerable<IConversionImpl<string, IPAddress>> impls) => Helper.ToXXX(str, Is, impls);
    }
}
