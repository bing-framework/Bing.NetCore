using System;
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
        /// 替换字符串-忽略大小写
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        public static string ReplaceIgnoreCase(this string original, string oldValue, string newValue) => Replace(original, oldValue, newValue, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 替换字符串-仅替换整个词组
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        public static string ReplaceOnlyWholePhrase(this string original, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(oldValue))
                return original;
            var index = original.IndexWholePhrase(oldValue);
            var lastIndex = 0;
            var buffer = new StringBuilder(original.Length);
            while (index >= 0)
            {
                buffer.Append(original, lastIndex, index - lastIndex);
                buffer.Append(newValue);
                lastIndex = index + oldValue.Length;
                index = original.IndexWholePhrase(oldValue, index + 1);
            }

            buffer.Append(original, lastIndex, original.Length - lastIndex);
            return buffer.ToString();
        }

        /// <summary>
        /// 替换字符串-仅替换首次出现字符串
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        public static string ReplaceFirstOccurrence(this string original, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(oldValue))
                return original;
            var index = original.IndexOfIgnoreCase(oldValue);
            if (index < 0)
                return original;
            if (index == 0)
                return newValue + original.Substring(oldValue.Length);
            return original.Substring(0, index) + newValue + original.Substring(index + oldValue.Length);
        }

        /// <summary>
        /// 替换字符串-仅替换最后一次出现字符串
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        public static string ReplaceLastOccurrence(this string original, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(oldValue))
                return original;
            var index = original.LastIndexOfIgnoreCase(oldValue);
            if (index < 0)
                return original;
            if (index == 0)
                return newValue + original.Substring(oldValue.Length);
            return original.Substring(0, index) + newValue + original.Substring(index + oldValue.Length);
        }

        /// <summary>
        /// 替换字符串-仅在结束时替换，忽略大小写
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        public static string ReplaceOnlyAtEndIgnoreCase(this string original, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(oldValue))
                return original;
            if (original.EndsWithIgnoreCase(oldValue))
                return original.Substring(0, original.Length - oldValue.Length) + newValue;
            return original;
        }

        /// <summary>
        /// 替换字符串
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        /// <param name="comparison">字符串比较类型</param>
        public static string Replace(this string original, string oldValue, string newValue, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(oldValue))
                return original;
            int index = -1, lastIndex = 0;
            var buffer = new StringBuilder(original.Length);
            while ((index = original.IndexOf(oldValue, index + 1, comparison)) >= 0)
            {
                buffer.Append(original, lastIndex, index - lastIndex);
                buffer.Append(newValue);
                lastIndex = index + oldValue.Length;
            }

            buffer.Append(original, lastIndex, original.Length - lastIndex);
            return buffer.ToString();
        }

        /// <summary>
        /// 替换字符串-递归替换所有
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        public static string ReplaceRecursive(this string original, string oldValue, string newValue)
        {
            const int maxTries = 1000;
            string ante;
            var res = original.Replace(oldValue, newValue);
            var i = 0;
            do
            {
                i++;
                ante = res;
                res = ante.Replace(oldValue, newValue);
            } while (ante != res || i > maxTries);
            return res;
        }

        /// <summary>
        /// 替换字符串-用空格替换指定字符
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="toReplace">替换字符</param>
        public static string ReplaceCharsWithSpace(this string original, params char[] toReplace)
        {
            var res = new StringBuilder(original);
            foreach (var replace in toReplace)
                res.Replace(replace, ' ');
            return res.ToString();
        }

        /// <summary>
        /// 替换字符-用指定字符替换数字
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="toReplace">替换字符</param>
        public static string ReplaceNumbersWith(this string original, char toReplace)
        {
            var res = new StringBuilder(original.Length);
            foreach (var character in original)
            {
                if (character.IsOn('1', '2', '3', '4', '5', '6', '7', '8', '9', '0'))
                    res.Append(toReplace);
                else
                    res.Append(character);
            }
            return res.ToString();
        }
    }
}
