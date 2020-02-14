using System.Globalization;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 基础类型扩展
    /// </summary>
    public static partial class BaseTypeExtensions
    {
        /// <summary>
        /// 获取数值
        /// </summary>
        /// <param name="c">字符</param>
        public static double GetNumericValue(this char c) => char.GetNumericValue(c);

        /// <summary>
        /// 获取Unicode类别
        /// </summary>
        /// <param name="c">字符</param>
        public static UnicodeCategory GetUnicodeCategory(this char c) => char.GetUnicodeCategory(c);

        /// <summary>
        /// 转换为小写字符
        /// </summary>
        /// <param name="c">字符</param>
        public static char ToLower(this char c) => char.ToLower(c);

        /// <summary>
        /// 转换为小写字符
        /// </summary>
        /// <param name="c">字符</param>
        /// <param name="culture">区域信息</param>
        public static char ToLower(this char c, CultureInfo culture) => char.ToLower(c, culture);

        /// <summary>
        /// 转换为小写字符。使用固定区域性的大小写规则
        /// </summary>
        /// <param name="c">字符</param>
        public static char ToLowerInvariant(this char c) => char.ToLowerInvariant(c);

        /// <summary>
        /// 转换为大写字符
        /// </summary>
        /// <param name="c">字符</param>
        public static char ToUpper(this char c) => char.ToUpper(c);

        /// <summary>
        /// 转换为大写字符
        /// </summary>
        /// <param name="c">字符</param>
        /// <param name="culture">区域信息</param>
        public static char ToUpper(this char c, CultureInfo culture) => char.ToUpper(c, culture);

        /// <summary>
        /// 转换为小写字符。使用固定区域性的大小写规则
        /// </summary>
        /// <param name="c">字符</param>
        public static char ToUpperInvariant(this char c) => char.ToUpperInvariant(c);

        /// <summary>
        /// 将 UTF-16 编码的代理项对的值转换为 Unicode 码位。
        /// </summary>
        /// <param name="highSurrogate">高代理项代码单位（即代码单位从 U+D800 到 U+DBFF）。</param>
        /// <param name="lowSurrogate">低代理项代码单位（即代码单位从 U+DC00 到 U+DFFF）。</param>
        public static int ConvertToUtf32(this char highSurrogate, char lowSurrogate) =>
            char.ConvertToUtf32(highSurrogate, lowSurrogate);

        /// <summary>
        /// 是否具有代理项代码单位
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsSurrogate(this char c) => char.IsSurrogate(c);

        /// <summary>
        /// 是否形成一个代理项对
        /// </summary>
        /// <param name="highSurrogate">要作为代理项对的高代理项进行计算的字符。</param>
        /// <param name="lowSurrogate">要作为代理项对的低代理项进行计算的字符。</param>
        public static bool IsSurrogatePair(this char highSurrogate, char lowSurrogate) =>
            char.IsSurrogatePair(highSurrogate, lowSurrogate);

        /// <summary>
        /// 是否高代理项
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsHighSurrogate(this char c) => char.IsHighSurrogate(c);

        /// <summary>
        /// 是否低代理项
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsLowSurrogate(this char c) => char.IsLowSurrogate(c);

        /// <summary>
        /// 重复拼接字符
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="repeatCount">重复次数</param>
        public static string Repeat(this char @this, int repeatCount) => new string(@this, repeatCount);
    }
}
