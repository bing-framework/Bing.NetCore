using System;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 相等-忽略大小写
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="toCheck">待检查文本</param>
        public static bool EqualsIgnoreCase(this string text, string toCheck) => string.Equals(text, toCheck, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 相等-忽略大小写
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="toCheck">待检查文本</param>
        public static bool EqualsToAnyIgnoreCase(this string text, params string[] toCheck) =>
            toCheck != null && toCheck.Any(t => string.Equals(text, t, StringComparison.OrdinalIgnoreCase));
    }
}
