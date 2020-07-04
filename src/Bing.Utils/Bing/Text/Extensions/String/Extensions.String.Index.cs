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
        /// 索引整个词组
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">待检查字符串</param>
        /// <param name="startIndex">起始索引</param>
        public static int IndexWholePhrase(this string text, string toCheck, int startIndex = 0)
        {
            if (string.IsNullOrEmpty(toCheck))
                throw new ArgumentNullException(nameof(toCheck), $"The parameter '{nameof(toCheck)}' cannot be null or empty.");
            while (startIndex <= text.Length)
            {
                var index = text.IndexOfIgnoreCase(startIndex, toCheck);
                if (index < 0)
                    return -1;
                var indexPreviousCar = index - 1;
                var indexNextCar = index + toCheck.Length;
                if ((index == 0 || !char.IsLetter(text[indexPreviousCar])) &&
                    (index + toCheck.Length == text.Length || !char.IsLetter(text[indexNextCar])))
                    return index;
                startIndex = index + toCheck.Length;
            }
            return -1;
        }

        /// <summary>
        /// 索引-忽略大小写
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">待检查字符串</param>
        public static int IndexOfIgnoreCase(this string text, string toCheck) =>
            text.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 索引-忽略大小写
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="startIndex">起始索引</param>
        /// <param name="toCheck">待检查字符串</param>
        public static int IndexOfIgnoreCase(this string text, int startIndex, string toCheck) =>
            text.IndexOf(toCheck, startIndex, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 最后一个索引-忽略大小写
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">待检查字符串</param>
        public static int LastIndexOfIgnoreCase(this string text, string toCheck) =>
            text.LastIndexOf(toCheck, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 最后一个索引-忽略大小写
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">待检查字符串</param>
        /// <param name="startIndex">起始索引</param>
        /// <param name="count">计数</param>
        public static int LastIndexOfIgnoreCase(this string text, string toCheck, int startIndex, int count) =>
            text.LastIndexOf(toCheck, startIndex, count, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 最后一个索引
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="toCheck">待检查字符串</param>
        public static int LastIndexOfAny(this string text, params string[] toCheck)
        {
            if (toCheck is null || toCheck.Length == 0)
                throw new ArgumentNullException(nameof(toCheck), $"The parameter '{nameof(toCheck)}' cannot be null or empty.");
            var res = -1;
            foreach (var checking in toCheck)
            {
                var index = text.LastIndexOf(checking, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && index > res)
                    res = index;
            }
            return res;
        }
    }
}
