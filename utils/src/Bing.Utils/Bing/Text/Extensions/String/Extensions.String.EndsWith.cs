using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Judgments;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 尾部匹配
        /// </summary>
        /// <param name="string">字符串</param>
        /// <param name="values">字符串数组</param>
        public static bool EndsWith(this string @string, params string[] values) =>
            StringJudgment.EndWithThese(@string, values);

        /// <summary>
        /// 尾部匹配
        /// </summary>
        /// <param name="string">字符串</param>
        /// <param name="values">字符串数组</param>
        public static bool EndsWith(this string @string, ICollection<string> values) =>
            StringJudgment.EndWithThese(@string, values);

        /// <summary>
        /// 尾部匹配-忽略大小写
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">待检查字符串</param>
        public static bool EndsWithIgnoreCase(this string text, string toCheck)
        {
            if (string.IsNullOrEmpty(toCheck))
                return true;
            return text.IsNullOrEmpty()
                ? toCheck.IsNullOrEmpty()
                : text.EndsWith(toCheck, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 尾部匹配-忽略大小写
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">待检查字符串</param>
        public static bool EndsWithAnyIgnoreCase(this string text, params string[] toCheck) =>
            EndsWithAnyIgnoreCase(text, (IEnumerable<string>)toCheck);

        /// <summary>
        /// 尾部匹配-忽略大小写
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">待检查字符串</param>
        public static bool EndsWithAnyIgnoreCase(this string text, IEnumerable<string> toCheck) =>
            !string.IsNullOrEmpty(text) &&
            toCheck.Any(check => text.EndsWith(check, StringComparison.OrdinalIgnoreCase));
    }
}
