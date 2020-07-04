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
        /// 切断。从左到右切断指定长度字符串
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="length">长度</param>
        /// <exception cref="ArgumentException"></exception>
        public static string Left(this string text, int length)
        {
            if (length < 0)
                throw new ArgumentException("Length 必须大于0", nameof(length));
            if (length == 0 || string.IsNullOrEmpty(text))
                return "";
            return length >= text.Length ? text : text.Substring(0, length);
        }

        /// <summary>
        /// 切断。从右到左切断指定长度字符串
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="length">长度</param>
        /// <exception cref="ArgumentException"></exception>
        public static string Right(this string text, int length)
        {
            if (length < 0)
                throw new ArgumentException("Length 必须大于0", nameof(length));
            if (length == 0 || string.IsNullOrEmpty(text))
                return "";
            var strLength = text.Length;
            return length >= strLength ? text : text.Substring(strLength - length, length);
        }
    }
}
