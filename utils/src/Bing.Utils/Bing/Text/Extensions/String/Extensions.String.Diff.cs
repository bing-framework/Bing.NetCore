using System;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 获取差异字符数
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">检查字符串</param>
        public static int DiffCharsCount(this string text, string toCheck)
        {
            GuardParameterForDiffOnlyOneChar(text, toCheck);
            var res = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != toCheck[i])
                    res++;
            }
            return res;
        }

        /// <summary>
        /// 获取差异字符数
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">检查字符串</param>
        public static int DiffCharsCountIgnoreCase(this string text,string toCheck)
        {
            GuardParameterForDiffOnlyOneChar(text, toCheck);
            var res = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (!text[i].EqualsIgnoreCase(toCheck[i]))
                    res++;
            }
            return res;
        }

        /// <summary>
        /// 仅用于一个字符的保护参数。防止字符串长度不相等
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="check">检查字符串</param>
        [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
        private static void GuardParameterForDiffOnlyOneChar(string text, string check)
        {
            if (text.Length != check.Length)
                throw new ArgumentException("The parameter for 'DiffOnlyOneChar' must have the same length");
        }

        /// <summary>
        /// 是否仅相差1个字符
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">检查字符串</param>
        public static bool DiffOnlyOneChar(this string text, string toCheck)
        {
            GuardParameterForDiffOnlyOneChar(text, toCheck);
            return text.DiffCharsCount(toCheck) == 1;
        }
    }
}
