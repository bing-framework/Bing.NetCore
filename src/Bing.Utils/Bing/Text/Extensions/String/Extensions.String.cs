using System;
using System.Collections.Generic;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 检查字符串是 null 还是 <see cref="string.Empty"/> 字符串
        /// </summary>
        /// <param name="str">字符串</param>
        public static bool IsNullOrEmpty(this string str) => string.IsNullOrEmpty(str);

        /// <summary>
        /// 检查字符串是 null、空还是仅由空白字符组成
        /// </summary>
        /// <param name="str">字符串</param>
        public static bool IsNullOrWhiteSpace(this string str) => string.IsNullOrWhiteSpace(str);

        /// <summary>
        /// 按换行符分割字符串为字符集合
        /// </summary>
        /// <param name="str">字符串</param>
        public static IEnumerable<string> EnumerateLines(this string str)
        {
            var index = 0;
            while (true)
            {
                var newIndex = str.IndexOf(Environment.NewLine, index, StringComparison.Ordinal);
                if (newIndex < 0)
                {
                    if (str.Length > index)
                        yield return str.Substring(index);
                    yield break;
                }

                var currentString = str.Substring(index, newIndex - index);
                index = newIndex + 2;
                yield return currentString;
            }
        }

        /// <summary>
        /// 转换为有效标识
        /// </summary>
        /// <param name="original">字符串</param>
        public static string ToValidIdentifier(this string original)
        {
            original = original.ToCapitalCase();
            if (original.Length == 0)
                return "_";
            var res = new StringBuilder(original.Length + 1);
            if (!char.IsLetter(original[0]) && original[0] != '_')
                res.Append('_');
            foreach (var c in original)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    res.Append(c);
                else
                    res.Append('_');
            }
            return res.ToString().ReplaceRecursive("__", "_").Trim('_');
        }
    }
}
