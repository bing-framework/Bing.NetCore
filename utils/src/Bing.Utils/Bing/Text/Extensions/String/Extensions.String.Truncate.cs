

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 截断字符串
        /// </summary>
        /// <param name="original">原始字符串</param>
        /// <param name="maxLength">最大长度</param>
        public static string Truncate(this string original, int maxLength)
        {
            if (string.IsNullOrEmpty(original) || maxLength == 0)
                return string.Empty;
            if (original.Length <= maxLength)
                return original;
            if (maxLength <= 3)
                return original.Substring(0, 2) + ".";
            return original.Substring(0, maxLength - 3) + "...";
        }
    }
}
