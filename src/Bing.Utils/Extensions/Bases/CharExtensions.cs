using System;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 字符(<see cref="Char"/>) 扩展
    /// </summary>
    public static class CharExtensions
    {
        #region In(判断当前字符是否在目标字符数组中)

        /// <summary>
        /// 判断当前字符是否在目标字符数组中
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="values">字符数组</param>
        /// <returns></returns>
        public static bool In(this char @this, params char[] values)
        {
            return Array.IndexOf(values, @this) != -1;
        }

        #endregion

        #region NotIn(判断当前字符是否不在目标字符数组中)

        /// <summary>
        /// 判断当前字符是否不在目标字符数组中
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="values">字符数组</param>
        /// <returns></returns>
        public static bool NotIn(this char @this, params char[] values)
        {
            return Array.IndexOf(values, @this) == -1;
        }

        #endregion

        #region Repeat(重复拼接字符)

        /// <summary>
        /// 重复拼接字符
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="repeatCount">重复数</param>
        /// <returns></returns>
        public static string Repeat(this char @this, int repeatCount)
        {
            return new string(@this, repeatCount);
        }

        #endregion

        #region GetAscii(获取ASCII编码)

        /// <summary>
        /// 获取ASCII编码
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static int GetAsciiCode(this char value)
        {
            byte[] bytes = Encoding.GetEncoding(0).GetBytes(value.ToString());
            if (bytes.Length == 1)
            {
                return bytes[0];
            }

            return (((bytes[0] * 0x100) + bytes[1]) - 0x10000);
        }

        #endregion

        #region IsChinese(是否中文字符串)

        /// <summary>
        /// 是否中文字符串
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsChinese(this char value)
        {
            return Regex.IsMatch(value.ToString(), "^[一-龥]$");
        }

        #endregion

        #region IsLine(是否行标识)

        /// <summary>
        /// 是否行标识
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsLine(this char value)
        {
            if (value != '\r')
            {
                return (value == '\n');
            }

            return true;
        }

        #endregion
    }
}
