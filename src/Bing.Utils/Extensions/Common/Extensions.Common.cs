using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 系统扩展 - 公共扩展
    /// </summary>
    public static partial class Extensions
    {
        #region SafeValue(安全获取值)
        /// <summary>
        /// 安全获取值，当值为null时，不会抛出异常
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="value">可空值</param>
        /// <returns></returns>
        public static T SafeValue<T>(this T? value) where T : struct
        {
            return value ?? default(T);
        }
        #endregion

        #region Value(获取枚举值)
        /// <summary>
        /// 获取枚举值
        /// </summary>
        /// <param name="instance">枚举实例</param>
        /// <returns></returns>
        public static int Value(this System.Enum instance)
        {
            return Utils.Helpers.Enum.GetValue(instance.GetType(), instance);
        }

        /// <summary>
        /// 获取枚举值
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="instance">枚举实例</param>
        /// <returns></returns>
        public static TResult Value<TResult>(this System.Enum instance)
        {
            return Utils.Helpers.Conv.To<TResult>(Value(instance));
        }
        #endregion

        #region Description(获取枚举描述)
        /// <summary>
        /// 获取枚举描述，使用<see cref="DescriptionAttribute"/>特性设置描述
        /// </summary>
        /// <param name="instance">枚举实例</param>
        /// <returns></returns>
        public static string Description(this System.Enum instance)
        {
            return Utils.Helpers.Enum.GetDescription(instance.GetType(), instance);
        }
        #endregion

        #region Join(转换为用分隔符连接的字符串)

        /// <summary>
        /// 转换为用分隔符连接的字符串
        /// </summary>
        /// <typeparam name="T">集合元素类型</typeparam>
        /// <param name="list">集合</param>
        /// <param name="quotes">引号，默认不带引号，范例：单引号"'"</param>
        /// <param name="separator">分隔符，默认使用逗号分隔</param>
        /// <returns></returns>
        public static string Join<T>(this IEnumerable<T> list, string quotes = "", string separator = ",")
        {
            return Utils.Helpers.Str.Join(list, quotes, separator);
        }

        #endregion

        #region IsMatch(是否匹配正则表达式)
        /// <summary>
        /// 确定所指定的正则表达式在指定的输入字符串中是否找到了匹配项
        /// </summary>
        /// <param name="value">要搜索匹配项的字符串</param>
        /// <param name="pattern">要匹配的正则表达式模式</param>
        /// <returns>如果正则表达式找到匹配项，则为 true；否则，为 false</returns>
        public static bool IsMatch(this string value, string pattern)
        {
            if (value == null)
            {
                return false;
            }
            return Regex.IsMatch(value, pattern);
        }
        /// <summary>
        /// 确定所指定的正则表达式在指定的输入字符串中找到匹配项
        /// </summary>
        /// <param name="value">要搜索匹配项的字符串</param>
        /// <param name="pattern">要匹配的正则表达式模式</param>
        /// <param name="options">规则</param>
        /// <returns>如果正则表达式找到匹配项，则为 true；否则，为 false</returns>
        public static bool IsMatch(this string value, string pattern, RegexOptions options)
        {
            if (value == null)
            {
                return false;
            }
            return Regex.IsMatch(value, pattern, options);
        }
        #endregion

        #region GetMatch(获取匹配项)
        /// <summary>
        /// 在指定的输入字符串中搜索指定的正则表达式的第一个匹配项
        /// </summary>
        /// <param name="value">要搜索匹配项的字符串</param>
        /// <param name="pattern">要匹配的正则表达式模式</param>
        /// <returns>一个对象，包含有关匹配项的信息</returns>
        public static string GetMatch(this string value, string pattern)
        {
            if (value.IsEmpty())
            {
                return string.Empty;
            }
            return Regex.Match(value, pattern).Value;
        }

        /// <summary>
        /// 在指定的输入字符串中搜索指定的正则表达式的所有匹配项的字符串集合
        /// </summary>
        /// <param name="value">要搜索匹配项的字符串</param>
        /// <param name="pattern">要匹配的正则表达式模式</param>
        /// <returns> 一个集合，包含有关匹配项的字符串值</returns>
        public static IEnumerable<string> GetMatchingValues(this string value, string pattern)
        {
            if (value.IsEmpty())
            {
                return new string[] { };
            }
            return GetMatchingValues(value, pattern, RegexOptions.None);
        }
        /// <summary>
        /// 使用正则表达式来确定一个给定的正则表达式模式的所有匹配的字符串返回的枚举
        /// </summary>
        /// <param name="value">输入字符串</param>
        /// <param name="pattern">正则表达式</param>
        /// <param name="options">比较规则</param>
        /// <returns>匹配字符串的枚举</returns>
        public static IEnumerable<string> GetMatchingValues(this string value, string pattern, RegexOptions options)
        {
            return from Match match in GetMatches(value, pattern, options) where match.Success select match.Value;
        }
        /// <summary>
        /// 使用正则表达式来确定指定的正则表达式模式的所有匹配项
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="pattern">正则表达式</param>
        /// <param name="options">比较规则</param>
        /// <returns></returns>
        public static MatchCollection GetMatches(this string value, string pattern, RegexOptions options)
        {
            return Regex.Matches(value, pattern, options);
        }
        #endregion
    }
}
