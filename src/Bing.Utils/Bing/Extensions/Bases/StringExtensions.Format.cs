using System.Globalization;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展 - 格式化
    /// </summary>
    public static partial class StringExtensions
    {
        #region FormatWith(格式化填充)

        /// <summary>
        /// 将指定字符串中的格式项替换为指定数组中相应对象的字符串表示形式
        /// </summary>
        /// <param name="format">字符串格式，占位符以{n}表示</param>
        /// <param name="args">用于填充占位符的参数</param>
        public static string FormatWith(this string format, params object[] args)
        {
            format.CheckNotNull("format");
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }

        #endregion
    }
}
