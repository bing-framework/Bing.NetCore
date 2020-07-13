using Bing.Collections;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 获取总英文字母数
        /// </summary>
        /// <param name="text">字符串</param>
        public static int TotalLetters(this string text) =>
            string.IsNullOrEmpty(text) ? 0 : text.ToCharArray().FindAll(char.IsLetter).Length;

        /// <summary>
        /// 获取总小写英文字母数
        /// </summary>
        /// <param name="text">字符串</param>
        public static int TotalLowerLetters(this string text) => string.IsNullOrEmpty(text)
            ? 0
            : text.ToCharArray().FindAll(x => char.IsLetter(x) && char.IsLower(x)).Length;

        /// <summary>
        /// 获取总大写英文字母数
        /// </summary>
        /// <param name="text">字符串</param>
        public static int TotalUpperLetters(this string text) => string.IsNullOrEmpty(text)
            ? 0
            : text.ToCharArray().FindAll(x => char.IsLetter(x) && char.IsUpper(x)).Length;

        /// <summary>
        /// 是否包含英文字母
        /// </summary>
        /// <param name="text">字符串</param>
        public static bool IncludeLetters(this string text) => text.IncludeLetters(0);

        /// <summary>
        /// 是否包含英文字母
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="minCount">最小数量</param>
        public static bool IncludeLetters(this string text, int minCount)
        {
            var count = 0;
            foreach (var c in text)
            {
                if (char.IsLetter(c))
                    count++;
                if (count >= minCount)
                    return true;
            }
            return false;
        }
    }
}
