using System;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展 - 正则表达式
    /// </summary>
    public static partial class StringExtensions
    {
        #region RegexSplit(根据正则表达式拆分为字符串数组)

        /// <summary>
        /// 根据正则表达式将字符串拆分为字符串数组
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="pattern">正则表达式</param>
        /// <param name="options">比较规则</param>
        /// <returns></returns>
        public static string[] RegexSplit(this string value, string pattern, RegexOptions options)
        {
            return Regex.Split(value, pattern, options);
        }

        #endregion

        #region GetWords(获取单词)

        /// <summary>
        /// 将给定的字符串拆分为单词并返回一个字符串数组
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string[] GetWords(this string value)
        {
            return value.RegexSplit(@"\W", RegexOptions.None);
        }

        /// <summary>
        /// 获取指定索引的单词
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="index">索引</param>
        /// <returns></returns>
        public static string GetWordByIndex(this string value, int index)
        {
            var words = value.GetWords();
            if (index < 0 || index > words.Length - 1)
            {
                throw new IndexOutOfRangeException(nameof(index));
            }

            return words[index];
        }

        #endregion

        #region SpaceOnUpper(大写字母添加空格)

        /// <summary>
        /// 在每个大写字母上添加空格
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string SpaceOnUpper(this string value)
        {
            return Regex.Replace(value, @"([A-Z])(?=[a-z])|(?<=[a-z])([A-Z]|[0-9]+)", " $1$2").TrimStart();
        }

        #endregion

        #region ReplaceWith(替换字符串)

        /// <summary>
        /// 使用正则表达式替换符合规则的字符串
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="pattern">正则表达式</param>
        /// <param name="replaceValue">替换值</param>
        /// <returns></returns>
        /// <example>
        /// 	<code>
        /// 		var s = "12345";
        /// 		var replaced = s.ReplaceWith(@"\d", m => string.Concat(" -", m.Value, "- "));
        /// 	</code>
        /// </example>
        public static string ReplaceWith(this string value, string pattern, string replaceValue)
        {
            return value.ReplaceWith(pattern, replaceValue, RegexOptions.None);
        }

        /// <summary>
        /// 使用正则表达式替换符合规则的字符串
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="pattern">正则表达式</param>
        /// <param name="replaceValue">替换值</param>
        /// <param name="options">比较规则</param>
        /// <returns></returns>
        /// <example>
        /// 	<code>
        /// 		var s = "12345";
        /// 		var replaced = s.ReplaceWith(@"\d", m => string.Concat(" -", m.Value, "- "));
        /// 	</code>
        /// </example>
        public static string ReplaceWith(this string value, string pattern, string replaceValue, RegexOptions options)
        {
            return Regex.Replace(value, pattern, replaceValue, options);
        }

        /// <summary>
        /// 使用正则表达式替换符合规则的字符串
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="pattern">正则表达式</param>
        /// <param name="evaluator">替换方法/Lambda表达式</param>
        /// <returns></returns>
        /// <example>
        /// 	<code>
        /// 		var s = "12345";
        /// 		var replaced = s.ReplaceWith(@"\d", m => string.Concat(" -", m.Value, "- "));
        /// 	</code>
        /// </example>
        public static string ReplaceWith(this string value, string pattern, MatchEvaluator evaluator)
        {
            return value.ReplaceWith(pattern, RegexOptions.None, evaluator);
        }

        /// <summary>
        /// 使用正则表达式替换符合规则的字符串
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="pattern">正则表达式</param>
        /// <param name="options">比较规则</param>
        /// <param name="evaluator">替换方法/Lambda表达式</param>
        /// <returns></returns>
        /// <example>
        /// 	<code>
        /// 		var s = "12345";
        /// 		var replaced = s.ReplaceWith(@"\d", m => string.Concat(" -", m.Value, "- "));
        /// 	</code>
        /// </example>
        public static string ReplaceWith(this string value, string pattern, RegexOptions options,
            MatchEvaluator evaluator)
        {
            return Regex.Replace(value, pattern, evaluator, options);
        }

        #endregion
    }
}
