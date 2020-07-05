using System;

namespace Bing.Conversions.Scale
{
    /// <summary>
    /// 二进制转换器
    /// </summary>
    public static class BinaryConverter
    {
        /// <summary>
        /// 转换为十进制
        /// </summary>
        /// <param name="bin">二进制</param>
        /// <example>in: 101110; out: 46</example>
        public static int ToDecimalism(string bin) => Convert.ToInt32(bin, 2);

        /// <summary>
        /// 转换为十六机制
        /// </summary>
        /// <param name="bin">二进制</param>
        /// <example>in: 101110; out: 2E</example>
        public static string ToHexadecimal(string bin) => DecimalismConverter.ToHexadecimal(ToDecimalism(bin));
    }
}
