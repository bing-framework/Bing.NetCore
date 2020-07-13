using System;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 截断字符串。子字符串从指定字符串之后开始
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="startText">起始字符串</param>
        public static string Substring(this string text, string startText)
        {
            var index = text.IndexOf(startText, StringComparison.Ordinal);
            if (index == -1)
                throw new ArgumentException($"Not found: '{startText}'.");
            return text.Substring(index);
        }

        /// <summary>
        /// 截断字符串。子字符串从指定字符串之前开始
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="from">开始字符串</param>
        public static string SubstringFrom(this string text, string from)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var index = text.IndexOfIgnoreCase(from);
            return index < 0 ? string.Empty : text.Substring(index + from.Length);
        }

        /// <summary>
        /// 截断字符串。子字符串从0开始到指定字符串之前
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="to">结尾字符串</param>
        public static string SubstringTo(this string text, string to)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var index = text.IndexOfIgnoreCase(to);
            return index < 0 ? string.Empty : text.Substring(0, index);
        }
    }
}
