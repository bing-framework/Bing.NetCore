using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
        /// 添加内容
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="sb">StringBuilder</param>
        /// <param name="values">值</param>
        /// <param name="separator">分隔符</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder AppendAll<T>(this StringBuilder sb, IEnumerable<T> values, string separator = null)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            Contract.EndContractBlock();
            if (values != null)
            {
                if (string.IsNullOrEmpty(separator))
                {
                    foreach (var value in values) 
                        sb.Append(value);
                }
                else
                {
                    foreach (var value in values) 
                        sb.AppendWithSeparator(separator, value);
                }
            }
            return sb;
        }

        /// <summary>
        /// 添加内容
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="sb">StringBuilder</param>
        /// <param name="values">值</param>
        /// <param name="separator">分隔符</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder AppendAll<T>(this StringBuilder sb, IEnumerable<T> values, in char separator)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            Contract.EndContractBlock();
            if (values != null)
            {
                foreach (var value in values) 
                    sb.AppendWithSeparator(in separator, value);
            }
            return sb;
        }

        /// <summary>
        /// 添加内容并以指定分隔符作为前缀
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="sb">StringBuilder</param>
        /// <param name="separator">分隔符</param>
        /// <param name="values">值</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder AppendWithSeparator<T>(this StringBuilder sb, string separator, params T[] values)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            if (values is null || values.Length == 0)
                throw new ArgumentException("Parameters missing.", nameof(values));
            Contract.EndContractBlock();
            if (!string.IsNullOrEmpty(separator) && sb.Length != 0)
                sb.Append(separator);
            sb.AppendAll(values);
            return sb;
        }

        /// <summary>
        /// 添加内容并以指定分隔符作为前缀
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="sb">StringBuilder</param>
        /// <param name="separator">分隔符</param>
        /// <param name="values">值</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static StringBuilder AppendWithSeparator<T>(this StringBuilder sb, in char separator, params T[] values)
        {
            if (sb is null)
                throw new ArgumentNullException(nameof(sb));
            if (values is null || values.Length == 0)
                throw new ArgumentException("Parameters missing.", nameof(values));
            Contract.EndContractBlock();
            if (sb.Length != 0)
                sb.Append(separator);
            sb.AppendAll(values);
            return sb;
        }

        /// <summary>
        /// 添加内容并换行
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="value">内容</param>
        /// <param name="parameters">参数</param>
        public static StringBuilder AppendLine(this StringBuilder sb, string value, params object[] parameters) => sb.AppendLine(string.Format(value, parameters));

        /// <summary>
        /// 添加数组内容
        /// </summary>
        /// <typeparam name="T">数组内容</typeparam>
        /// <param name="sb">StringBuilder</param>
        /// <param name="separator">分隔符</param>
        /// <param name="values">数组内容</param>
        public static StringBuilder AppendJoin<T>(this StringBuilder sb, string separator, params T[] values) => sb.Append(string.Join(separator, values));

        /// <summary>
        /// 根据条件添加内容
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="condition">拼接条件</param>
        /// <param name="value">内容</param>
        public static StringBuilder AppendIf(this StringBuilder sb, bool condition, object value)
        {
            if (condition)
                sb.Append(value.ToString());
            return sb;
        }

        /// <summary>
        /// 根据条件添加内容
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="condition">拼接条件</param>
        /// <param name="value">内容</param>
        /// <param name="parameters">参数</param>
        public static StringBuilder AppendFormatIf(this StringBuilder sb, bool condition, string value,
            params object[] parameters)
        {
            if (condition)
                sb.AppendFormat(value, parameters);
            return sb;
        }

        /// <summary>
        /// 根据条件添加内容并换行
        /// </summary>
        /// <param name="sb">StringBuiler</param>
        /// <param name="condition">拼接条件</param>
        /// <param name="value">内容</param>
        public static StringBuilder AppendLineIf(this StringBuilder sb, bool condition, object value)
        {
            if (condition)
                sb.AppendLine(value.ToString());
            return sb;
        }

        /// <summary>
        /// 根据条件添加内容并换行
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="condition">拼接条件</param>
        /// <param name="value">内容</param>
        /// <param name="parameters">参数</param>
        public static StringBuilder AppendLine(this StringBuilder sb, bool condition, string value,
            params object[] parameters)
        {
            if (condition)
                sb.AppendFormat(value, parameters).AppendLine();
            return sb;
        }
    }
}
