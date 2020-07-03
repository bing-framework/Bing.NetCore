using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Bing
{
    /// <summary>
    /// 字符(<see cref="char"/>) 扩展
    /// </summary>
    public static class CharExtensions
    {
        #region Numeric

        /// <summary>
        /// 获取数值
        /// </summary>
        /// <param name="c">字符</param>
        public static double GetNumericValue(this char c) => char.GetNumericValue(c);

        #endregion

        #region Repeat

        /// <summary>
        /// 重复拼接字符
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="repeatCount">重复次数</param>
        public static string Repeat(this char @this, int repeatCount) => new string(@this, repeatCount);

        #endregion

        #region Between

        /// <summary>
        /// 是否在指定范围内
        /// </summary>
        /// <param name="char">字符</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static bool IsBetween(this char @char, char min, char max)
        {
            var result = Fix(min, max);
            return @char >= result.min && @char <= result.max;
        }

        /// <summary>
        /// 修复
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        private static (char min, char max) Fix(char min, char max) => min >= max ? (max, min) : (min, max);

        #endregion

        #region In

        /// <summary>
        /// 是否在指定数组内
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="values">数组</param>
        public static bool In(this char @this, params char[] values) => Array.IndexOf(values, @this) != -1;

        /// <summary>
        /// 是否不在指定数组内
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="values">数组</param>
        public static bool NotIn(this char @this, params char[] values) => Array.IndexOf(values, @this) == -1;

        #endregion

        #region Is

        /// <summary>
        /// 是否空白字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsWhiteSpace(this char c) => char.IsWhiteSpace(c);

        /// <summary>
        /// 是否控制字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsControl(this char c) => char.IsControl(c);

        /// <summary>
        /// 是否十进制数字字符。仅支持十进制数字
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsDigit(this char c) => char.IsDigit(c);

        /// <summary>
        /// 是否英文字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsLetter(this char c) => char.IsLetter(c);

        /// <summary>
        /// 是否英文或数字字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsLetterOrDigit(this char c) => char.IsLetterOrDigit(c);

        /// <summary>
        /// 是否小写英文字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsLower(this char c) => char.IsLower(c);

        /// <summary>
        /// 是否数字字符。支持所有数字字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsNumber(this char c) => char.IsNumber(c);

        /// <summary>
        /// 是否标点符号
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsPunctuation(this char c) => char.IsPunctuation(c);

        /// <summary>
        /// 是否分隔符号
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsSeparator(this char c) => char.IsSeparator(c);

        /// <summary>
        /// 是否符号字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsSymbol(this char c) => char.IsSymbol(c);

        #endregion

        #region Is Surrogate

        /// <summary>
        /// 是否Unicode代理项代码单元
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsSurrogate(this char c) => char.IsSurrogate(c);

        /// <summary>
        /// 是否一个有效的代理对
        /// </summary>
        /// <param name="highSurrogate">高代理项代码单元</param>
        /// <param name="lowSurrogate">低代理项代码单元</param>
        public static bool IsSurrogatePair(this char highSurrogate, char lowSurrogate) => char.IsSurrogatePair(highSurrogate, lowSurrogate);

        /// <summary>
        /// 是否高代理项代码单元
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsHighSurrogate(this char c) => char.IsHighSurrogate(c);

        /// <summary>
        /// 是否低代理项代码单元
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsLowSurrogate(this char c) => char.IsLowSurrogate(c);

        #endregion

        #region Case

        #region ToLower(转换为小写字符)

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

        #endregion

        #region ToUpper(转换为大写字符)

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

        #endregion

        #endregion

        #region Equals with IgnoreCase

        /// <summary>
        /// 相等-忽略大小写
        /// </summary>
        /// <param name="text">字符</param>
        /// <param name="toCheck">待检查字符</param>
        public static bool EqualsIgnoreCase(this char text, char toCheck) => char.ToUpper(text) == char.ToUpper(toCheck);

        /// <summary>
        /// 相等-忽略大小写
        /// </summary>
        /// <param name="text">字符</param>
        /// <param name="toCheck">待检查字符</param>
        public static bool EqualsIgnoreCase(this char? text, char toCheck) => text != null && char.ToUpper(text.Value) == char.ToUpper(toCheck);

        #endregion

        #region To

        /// <summary>
        /// 转换一个从小到大的字符集合
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="toCharacter">目标字符</param>
        public static IEnumerable<char> To(this char @this, char toCharacter)
        {
            var reverseRequired = @this > toCharacter;
            var first = reverseRequired ? toCharacter : @this;
            var last = reverseRequired ? @this : toCharacter;
            var result = Enumerable.Range(first, last - first + 1).Select(charCode => (char)charCode);
            if (reverseRequired) 
                result = result.Reverse();
            return result;
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <param name="c">字符</param>
        public static string ToString(this char c) => char.ToString(c);

        #endregion

        #region Utf32

        /// <summary>
        /// 将 UTF-16 编码的代理项对的值转换为 Unicode 码位。
        /// </summary>
        /// <param name="highSurrogate">高代理项代码单位（即代码单位从 U+D800 到 U+DBFF）。</param>
        /// <param name="lowSurrogate">低代理项代码单位（即代码单位从 U+DC00 到 U+DFFF）。</param>
        public static int ConvertToUtf32(this char highSurrogate, char lowSurrogate) =>
            char.ConvertToUtf32(highSurrogate, lowSurrogate);

        #endregion

        #region UnicodeCategory

        /// <summary>
        /// 获取Unicode类别
        /// </summary>
        /// <param name="c">字符</param>
        public static UnicodeCategory GetUnicodeCategory(this char c) => char.GetUnicodeCategory(c);

        #endregion
    }
}
