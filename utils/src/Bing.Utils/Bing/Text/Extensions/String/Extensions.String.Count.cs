using System;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 计算行数
        /// </summary>
        /// <param name="str">字符串</param>
        public static int CountLines(this string str)
        {
            int index = 0, lines = 0;
            while (true)
            {
                var newIndex = str.IndexOf(Environment.NewLine, index, StringComparison.Ordinal);
                if (newIndex < 0)
                {
                    if (str.Length > index)
                        lines++;
                    return lines;
                }

                index = newIndex + 2;
                lines++;
            }
        }

        /// <summary>
        /// 计算指定字符出现次数
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="toCheck">待检查字符</param>
        public static int CountOccurrences(this string str, char toCheck) => CountOccurrences(str, toCheck.ToString());

        /// <summary>
        /// 计算指定字符串出现次数
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="toCheck">待检查字符串</param>
        public static int CountOccurrences(this string str, string toCheck)
        {
            if (string.IsNullOrEmpty(toCheck))
                return 0;
            int res = 0, posIni = 0;
            while ((posIni=str.IndexOfIgnoreCase(posIni,toCheck))!=-1)
            {
                posIni += toCheck.Length;
                res++;
            }
            return res;
        }
    }
}
