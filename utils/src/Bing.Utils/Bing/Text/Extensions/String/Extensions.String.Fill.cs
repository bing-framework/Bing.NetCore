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
        /// 填充
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="values">填充对象</param>
        public static string Fill(this string text, params object[] values)
        {
            var s=text.Replace("\\n",Environment.NewLine)
                .Replace("<br>", Environment.NewLine)
                .Replace("<BR>", Environment.NewLine);
            return string.Format(s, values);
        }
    }
}
