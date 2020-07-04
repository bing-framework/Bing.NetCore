using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.Collections;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 仅返回数字
        /// </summary>
        /// <param name="text">字符串</param>
        public static string OnlyDigits(this string text) => OnlyDigits(text, null);

        /// <summary>
        /// 仅返回数组以及例外字符
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="exceptions">例外字符</param>
        public static string OnlyDigits(this string text, IEnumerable<char> exceptions)
        {
            var res = new StringBuilder();
            foreach (var c in text)
            {
                if (char.IsDigit(c) || (exceptions != null && exceptions.Contains(c)))
                    res.Append(c);
            }
            return res.ToString();
        }

        /// <summary>
        /// 获取总数字字符数
        /// </summary>
        /// <param name="text">字符串</param>
        public static int TotalDigits(this string text) =>
            string.IsNullOrEmpty(text) ? 0 : text.ToCharArray().FindAll(char.IsDigit).Length;

        /// <summary>
        /// 包含。仅包含数字
        /// </summary>
        /// <param name="text">字符串</param>
        public static bool ContainsOnlyDigits(this string text) => text.All(char.IsDigit);

        /// <summary>
        /// 不包含。不包含数字
        /// </summary>
        /// <param name="text">字符串</param>
        public static bool NonContainsDigits(this string text) => text.All(c => !char.IsDigit(c));

        /// <summary>
        /// 包含。是否包含数字
        /// </summary>
        /// <param name="text">字符串</param>
        public static bool ContainsDigits(this string text) => text.Any(char.IsDigit);

        /// <summary>
        /// 是否包含数字
        /// </summary>
        /// <param name="text">字符串</param>
        public static bool IncludeDigits(this string text) => text.IncludeDigits(0);

        /// <summary>
        /// 是否包含数字
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="minCount">最小数量</param>
        public static bool IncludeDigits(this string text, int minCount)
        {
            var count = 0;
            foreach (var c in text)
            {
                if (char.IsDigit(c))
                    count++;
                if (count >= minCount)
                    return true;
            }
            return false;
        }
    }
}
