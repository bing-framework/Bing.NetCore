using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bing.Judgments
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 判断
    /// </summary>
    public static class StringJudgment
    {
        /// <summary>
        /// 是否以指定字符串开头
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="values">字符串集合</param>
        public static bool StartWithThese(string str, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(str) || values is null || values.Any(string.IsNullOrWhiteSpace))
                return false;
            return values.Any(str.StartsWith);
        }

        /// <summary>
        /// 是否以指定字符串开头
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="values">字符串集合</param>
        public static bool StartWithThese(string str, ICollection<string> values)
        {
            if (string.IsNullOrWhiteSpace(str) || values is null || !values.Any())
                return false;
            return StartWithThese(str, values.ToArray());
        }

        /// <summary>
        /// 是否以指定字符串结尾
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="values">字符串集合</param>
        public static bool EndWithThese(string str, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(str) || values is null || values.Any(string.IsNullOrWhiteSpace))
                return false;
            return values.Any(str.EndsWith);
        }

        /// <summary>
        /// 是否以指定字符串结尾
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="values">字符串集合</param>
        public static bool EndWithThese(string str, ICollection<string> values)
        {
            if (string.IsNullOrWhiteSpace(str) || values is null || !values.Any())
                return false;
            return EndWithThese(str, values.ToArray());
        }

        /// <summary>
        /// WebUrl 正则表达式
        /// </summary>
        private static readonly Regex WebUrlExpressionSchema = new Regex(@"(http|https)://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?",
            RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// 是否WebUrl
        /// </summary>
        /// <param name="str">字符串</param>
        public static bool IsWebUrl(string str) =>
            !string.IsNullOrWhiteSpace(str) && WebUrlExpressionSchema.IsMatch(str);

        /// <summary>
        /// Email 正则表达式
        /// </summary>
        private static readonly Regex EmailExpressionSchema = new Regex(@"^([0-9a-zA-Z]+[-._+&])*[0-9a-zA-Z]+@([-0-9a-zA-Z]+[.])+[a-zA-Z]{2,6}$",
            RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// 是否Email
        /// </summary>
        /// <param name="str">字符串</param>
        public static bool IsEmail(string str) => !string.IsNullOrWhiteSpace(str) && EmailExpressionSchema.IsMatch(str);

        /// <summary>
        /// 是否包含中文字符
        /// </summary>
        /// <param name="str">字符串</param>
        public static bool ContainsChineseCharacters(string str) => !string.IsNullOrWhiteSpace(str) && RegexJudgment.IsMatch(str, "[\u4e00-\u9fa5]+");

        /// <summary>
        /// 是否包含数字字符
        /// </summary>
        /// <param name="str">字符串</param>
        public static bool ContainsNumber(string str) =>
            !string.IsNullOrWhiteSpace(str) && RegexJudgment.IsMatch(str, "[0-9]+");
    }
}
