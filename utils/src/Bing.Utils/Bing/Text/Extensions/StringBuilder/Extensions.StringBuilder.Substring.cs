using System;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// <see cref="StringBuilder"/> 扩展
    /// </summary>
    public static partial class StringBuilderExtensions
    {
        /// <summary>
        /// 返回<see cref="StringBuilder"/>从起始位置指定长度的字符串
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="start">起始位置</param>
        /// <param name="length">长度</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public static string SubString(this StringBuilder sb, int start, int length)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            if (start + length > sb.Length)
                throw new IndexOutOfRangeException("超出字符串索引长度");
            var chars = new char[length];
            for (var i = 0; i < length; i++)
                chars[i] = sb[start + i];
            return new string(chars);
        }
    }
}
