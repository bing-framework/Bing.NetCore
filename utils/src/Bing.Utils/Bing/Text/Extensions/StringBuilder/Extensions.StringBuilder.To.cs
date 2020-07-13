using System;
using System.Collections.Generic;
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
        /// 转换为 <see cref="StringBuilder"/>
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="source">数据源</param>
        public static StringBuilder ToStringBuilder<T>(this in ReadOnlySpan<T> source)
        {
            var len = source.Length;
            var sb = new StringBuilder(len);
            for (var i = 0; i < len; i++)
                sb.Append(source[i]);
            return sb;
        }

        /// <summary>
        /// 转换为 <see cref="StringBuilder"/>
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="separator">分隔符</param>
        public static StringBuilder ToStringBuilder<T>(this in ReadOnlySpan<T> source, in string separator)
        {
            var len = source.Length;
            if (len < 2 || string.IsNullOrEmpty(separator))
                return ToStringBuilder(source);
            var sb = new StringBuilder(2 * len - 1);
            sb.Append(source[0]);
            for (var i = 1; i < len; i++)
            {
                sb.Append(separator);
                sb.Append(source[i]);
            }
            return sb;
        }

        /// <summary>
        /// 转换为 <see cref="StringBuilder"/>
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="separator">分隔符</param>
        public static StringBuilder ToStringBuilder<T>(this in ReadOnlySpan<T> source, in char separator)
        {
            var len = source.Length;
            if (len < 2)
                return ToStringBuilder(source);
            var sb = new StringBuilder(2 * len - 1);
            sb.Append(source[0]);
            for (var i = 1; i < len; i++)
            {
                sb.Append(separator);
                sb.Append(source[i]);
            }
            return sb;
        }

        /// <summary>
        /// 转换为 <see cref="StringBuilder"/>
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="source">数据源</param>
        public static StringBuilder ToStringBuilder<T>(this IEnumerable<T> source)
        {
            var sb = new StringBuilder();
            foreach (var s in source) 
                sb.Append(s);
            return sb;
        }

        /// <summary>
        /// 转换为 <see cref="StringBuilder"/>
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="separator">分隔符</param>
        public static StringBuilder ToStringBuilder<T>(this IEnumerable<T> source, in string separator)
        {
            var sb = new StringBuilder();
            var first = true;
            foreach (var s in source)
            {
                if (first)
                    first = false;
                else
                    sb.Append(separator);
                sb.Append(s);
            }
            return sb;
        }

        /// <summary>
        /// 转换为 <see cref="StringBuilder"/>
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="separator">分隔符</param>
        public static StringBuilder ToStringBuilder<T>(this IEnumerable<T> source, in char separator)
        {
            var sb = new StringBuilder();
            var first = true;
            foreach (var s in source)
            {
                if (first)
                    first = false;
                else
                    sb.Append(separator);
                sb.Append(s);
            }
            return sb;
        }
    }
}
