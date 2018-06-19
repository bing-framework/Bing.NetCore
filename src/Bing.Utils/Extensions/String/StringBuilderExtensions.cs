using System;
using System.Text;

namespace Bing.Utils.Extensions
{
    /// <summary>
    /// StringBuilder 扩展
    /// </summary>
    public static class StringBuilderExtensions
    {
        #region TrimStart(去除StringBuilder开头指定值)

        /// <summary>
        /// 去除<see cref="StringBuilder"/>开头空格
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <returns></returns>
        public static StringBuilder TrimStart(this StringBuilder sb)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }
            return sb.TrimStart(' ');
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>开头指定字符
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="c">字符</param>
        /// <returns></returns>
        public static StringBuilder TrimStart(this StringBuilder sb,char c)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (sb.Length == 0)
            {
                return sb;
            }

            while (c.Equals(sb[0]))
            {
                sb.Remove(0, 1);
            }

            return sb;
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>开头指定字符数组
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="chars">字符数组</param>
        /// <returns></returns>
        public static StringBuilder TrimStart(this StringBuilder sb, char[] chars)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (chars == null)
            {
                throw new ArgumentNullException(nameof(chars));
            }

            return sb.TrimStart(new string(chars));
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>开头指定字符串
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="str">字符串</param>
        /// <returns></returns>
        public static StringBuilder TrimStart(this StringBuilder sb, string str)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (string.IsNullOrEmpty(str) || sb.Length == 0 || str.Length > sb.Length)
            {
                return sb;
            }

            while (sb.SubString(0,str.Length).Equals(str))
            {
                sb.Remove(0, str.Length);
                if (str.Length > sb.Length)
                {
                    break;
                }
            }

            return sb;
        }

        #endregion

        #region TrimEnd(去除StringBuilder尾部指定值)

        /// <summary>
        /// 去除<see cref="StringBuilder"/>尾部空格
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <returns></returns>
        public static StringBuilder TrimEnd(this StringBuilder sb)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }
            return sb.TrimEnd(' ');
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>尾部指定字符
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="c">字符</param>
        /// <returns></returns>
        public static StringBuilder TrimEnd(this StringBuilder sb, char c)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (sb.Length == 0)
            {
                return sb;
            }

            while (c.Equals(sb[sb.Length-1]))
            {
                sb.Remove(sb.Length-1, 1);
            }

            return sb;
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>尾部指定字符数组
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="chars">字符数组</param>
        /// <returns></returns>
        public static StringBuilder TrimEnd(this StringBuilder sb, char[] chars)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (chars == null)
            {
                throw new ArgumentNullException(nameof(chars));
            }

            return sb.TrimEnd(new string(chars));
        }

        /// <summary>
        /// 去除<see cref="StringBuilder"/>尾部指定字符串
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="str">字符串</param>
        /// <returns></returns>
        public static StringBuilder TrimEnd(this StringBuilder sb, string str)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (string.IsNullOrEmpty(str) || sb.Length == 0 || str.Length > sb.Length)
            {
                return sb;
            }

            while (sb.SubString(sb.Length-str.Length, str.Length).Equals(str))
            {
                sb.Remove(sb.Length - str.Length, str.Length);
                if (sb.Length < str.Length)
                {
                    break;
                }
            }

            return sb;
        }

        #endregion

        #region Trim(去除StringBuilder两端的空格)

        /// <summary>
        /// 去除<see cref="StringBuilder"/>两端的空格
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <returns></returns>
        public static StringBuilder Trim(this StringBuilder sb)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (sb.Length == 0)
            {
                return sb;
            }

            return sb.TrimEnd().TrimStart();
        }

        #endregion

        #region SubString(返回StringBuilder从起始位置指定长度的字符串)

        /// <summary>
        /// 返回<see cref="StringBuilder"/>从起始位置指定长度的字符串
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="start">起始位置</param>
        /// <param name="length">长度</param>
        /// <returns></returns>
        public static string SubString(this StringBuilder sb, int start, int length)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (start + length > sb.Length)
            {
                throw new IndexOutOfRangeException("超出字符串索引长度");
            }
            char[] chars=new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = sb[start + i];
            }
            return new string(chars);
        }

        #endregion

        #region AppendLine(添加内容并换行)

        /// <summary>
        /// 添加内容并换行
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="value">内容</param>
        /// <param name="parameters">参数</param>
        /// <returns></returns>
        public static StringBuilder AppendLine(this StringBuilder sb, string value, params object[] parameters)
        {
            return sb.AppendLine(string.Format(value, parameters));
        }

        #endregion

        #region AppendJoin(添加数组内容)

        /// <summary>
        /// 添加数组内容
        /// </summary>
        /// <typeparam name="T">数组内容</typeparam>
        /// <param name="sb">StringBuilder</param>
        /// <param name="separator">分隔符</param>
        /// <param name="values">数组内容</param>
        /// <returns></returns>
        public static StringBuilder AppendJoin<T>(this StringBuilder sb, string separator, params T[] values)
        {
            sb.Append(string.Join(separator, values));
            return sb;
        }

        #endregion

        #region AppendIf(根据条件添加内容)

        /// <summary>
        /// 根据条件添加内容
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="condition">拼接条件</param>
        /// <param name="value">内容</param>
        /// <returns></returns>
        public static StringBuilder AppendIf(this StringBuilder sb, bool condition, object value)
        {
            if (condition)
            {
                sb.Append(value.ToString());
            }

            return sb;
        }

        #endregion

        #region AppendFormatIf(根据条件添加内容)

        /// <summary>
        /// 根据条件添加内容
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="condition">拼接条件</param>
        /// <param name="value">内容</param>
        /// <param name="parameters">参数</param>
        /// <returns></returns>
        public static StringBuilder AppendFormatIf(this StringBuilder sb, bool condition, string value,
            params object[] parameters)
        {
            if (condition)
            {
                sb.AppendFormat(value, parameters);
            }

            return sb;
        }

        #endregion

        #region AppendLineIf(根据条件添加内容并换行)

        /// <summary>
        /// 根据条件添加内容并换行
        /// </summary>
        /// <param name="sb">StringBuiler</param>
        /// <param name="condition">拼接条件</param>
        /// <param name="value">内容</param>
        /// <returns></returns>
        public static StringBuilder AppendLineIf(this StringBuilder sb, bool condition, object value)
        {
            if (condition)
            {
                sb.AppendLine(value.ToString());
            }

            return sb;
        }

        /// <summary>
        /// 根据条件添加内容并换行
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="condition">拼接条件</param>
        /// <param name="value">内容</param>
        /// <param name="parmaeters">参数</param>
        /// <returns></returns>
        public static StringBuilder AppendLine(this StringBuilder sb, bool condition, string value,
            params object[] parmaeters)
        {
            if (condition)
            {
                sb.AppendFormat(value, parmaeters).AppendLine();
            }

            return sb;
        }

        #endregion
    }
}
