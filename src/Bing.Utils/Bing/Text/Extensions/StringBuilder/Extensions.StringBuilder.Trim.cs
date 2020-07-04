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
        /// 去除<see cref="StringBuilder"/>开头空格
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder TrimStart(this StringBuilder sb)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            return sb.TrimStart(' ');
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>开头指定字符
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="c">字符</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder TrimStart(this StringBuilder sb, char c)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            if (sb.Length == 0)
                return sb;
            while (c.Equals(sb[0]))
                sb.Remove(0, 1);
            return sb;
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>开头指定字符数组
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="chars">字符数组</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder TrimStart(this StringBuilder sb, char[] chars)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            if (chars == null)
                throw new ArgumentNullException(nameof(chars));
            return sb.TrimStart(new string(chars));
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>开头指定字符串
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="str">字符串</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder TrimStart(this StringBuilder sb, string str)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            if (string.IsNullOrEmpty(str) || sb.Length == 0 || str.Length > sb.Length)
                return sb;
            while (sb.SubString(0, str.Length).Equals(str))
            {
                sb.Remove(0, str.Length);
                if (str.Length > sb.Length)
                    break;
            }
            return sb;
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>尾部空格
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder TrimEnd(this StringBuilder sb)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            return sb.TrimEnd(' ');
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>尾部指定字符
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="c">字符</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder TrimEnd(this StringBuilder sb, char c)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            if (sb.Length == 0)
                return sb;
            while (c.Equals(sb[sb.Length - 1]))
                sb.Remove(sb.Length - 1, 1);
            return sb;
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>尾部指定字符数组
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="chars">字符数组</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder TrimEnd(this StringBuilder sb, char[] chars)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            if (chars is null)
                throw new ArgumentNullException(nameof(chars));
            return sb.TrimEnd(new string(chars));
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>尾部指定字符串
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="str">字符串</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder TrimEnd(this StringBuilder sb, string str)
        {
            if (sb == null)
                throw new ArgumentNullException(nameof(sb));
            if (string.IsNullOrEmpty(str) || sb.Length == 0 || str.Length > sb.Length)
                return sb;
            while (sb.SubString(sb.Length - str.Length, str.Length).Equals(str))
            {
                sb.Remove(sb.Length - str.Length, str.Length);
                if (sb.Length < str.Length)
                    break;
            }

            return sb;
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>两端的空格
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder Trim(this StringBuilder sb)
        {
            if (sb == null)
                throw new ArgumentNullException(nameof(sb));
            if (sb.Length == 0)
                return sb;
            return sb.TrimEnd().TrimStart();
        }
    }
}
