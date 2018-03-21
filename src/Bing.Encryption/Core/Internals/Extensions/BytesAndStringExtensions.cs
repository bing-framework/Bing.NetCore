using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Encryption.Core.Internals.Extensions
{
    /// <summary>
    /// 字节数组及字符串扩展
    /// </summary>
    internal static class BytesAndStringExtensions
    {
        /// <summary>
        /// 将字节数组转换成16进制字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        internal static string ToHexString(this byte[] bytes)
        {
            var sb=new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 将16进制字符串转换成字节数组
        /// </summary>
        /// <param name="hex">16进制字符串</param>
        /// <returns></returns>
        internal static byte[] ToBytes(this string hex)
        {
            if (hex.Length == 0)
            {
                return new byte[]{0};
            }

            if (hex.Length % 2 == 1)
            {
                hex = "0" + hex;
            }
            byte[] result=new byte[hex.Length/2];
            for (int i = 0; i < hex.Length / 2; i++)
            {
                result[i] = byte.Parse(hex.Substring(2 * i, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
            }

            return result;
        }
    }
}
