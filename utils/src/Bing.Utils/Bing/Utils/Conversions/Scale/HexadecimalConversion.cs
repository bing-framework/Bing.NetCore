using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bing.Utils.Conversions.Internals;

namespace Bing.Utils.Conversions.Scale
{
    /// <summary>
    /// 十六进制转换操作
    /// </summary>
    public static class HexadecimalConversion
    {
        /// <summary>
        /// 转换为十进制
        /// </summary>
        /// <param name="hex">十六进制字符串</param>
        /// <example>in: 2E; out: 46</example>
        public static int ToDecimalism(string hex) => Convert.ToInt32(hex, 16);

        /// <summary>
        /// 转换为二进制字符串
        /// </summary>
        /// <param name="hex">十六进制字符串</param>
        /// <example>in: 2E; out: 101110</example>
        public static string ToBinary(string hex) => DecimalismConversion.ToBinary(ToDecimalism(hex));

        /// <summary>
        /// 转换为字节数组
        /// </summary>
        /// <param name="hex">十六进制字符串</param>
        /// <example>in: 2E3D; out: result[0] is 46, result[1] is 61</example>
        public static byte[] ToBytes(string hex)
        {
            var mc = Regex.Matches(hex, @"(?i)[\da-f]{2}");
            return (from Match m in mc select Convert.ToByte(m.Value, 16)).ToArray();
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <param name="hex">十六进制字符串</param>
        /// <param name="encoding">编码方式</param>
        public static string ToString(string hex, Encoding encoding = null)
        {
            hex = hex.Replace(" ", "");
            if (string.IsNullOrWhiteSpace(hex))
                return "";
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < hex.Length; i += 2)
            {
                if (!byte.TryParse(hex.Substring(i, 2), NumberStyles.HexNumber, null, out bytes[i / 2]))
                    bytes[i / 2] = 0;
            }
            return encoding.Fixed().GetString(bytes);
        }

        /// <summary>
        /// 将普通字符串转换为十六进制字符串
        /// </summary>
        /// <param name="str">普通字符串</param>
        /// <param name="encoding">编码方式</param>
        /// <example>in: A; out: 1000001</example>
        public static string FromString(string str, Encoding encoding = null) => BitConverter.ToString(encoding.Fixed().GetBytes(str)).Replace("-", " ");
    }
}
