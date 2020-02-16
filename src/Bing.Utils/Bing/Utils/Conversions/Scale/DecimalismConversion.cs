using System;

namespace Bing.Utils.Conversions.Scale
{
    /// <summary>
    /// 十进制转换操作
    /// </summary>
    public static class DecimalismConversion
    {
        /// <summary>
        /// 转换为二进制字符串
        /// </summary>
        /// <param name="dec">十进制</param>
        /// <example>in: 46; out: 101110</example>
        public static string ToBinary(int dec) => Convert.ToString(dec, 2);

        /// <summary>
        /// 转换为十六进制字符串
        /// </summary>
        /// <param name="dec">十进制</param>
        /// <example>in: 46; out: 2E</example>
        public static string ToHexadecimal(int dec) => Convert.ToString(dec, 16).ToUpper();

        /// <summary>
        /// 转换为十六进制字符串
        /// </summary>
        /// <param name="dec">十进制</param>
        /// <param name="formatLength">格式化长度</param>
        /// <example>in: 46, 4; out: 002E</example>
        public static string ToHexadecimal(int dec, int formatLength)
        {
            var hex = ToHexadecimal(dec);
            return hex.Length > formatLength ? hex : hex.PadLeft(formatLength, '0');
        }
    }
}
