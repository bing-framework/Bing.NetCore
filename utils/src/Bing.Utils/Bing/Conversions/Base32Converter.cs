using System;
using System.Text;

namespace Bing.Conversions
{
    /// <summary>
    /// Base32 转换器
    /// </summary>
    public static class Base32Converter
    {
        /// <summary>
        /// 转换为Base32字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static string ToBase32String(byte[] bytes)
        {
            if (bytes is null || bytes.Length == 0)
                throw new ArgumentNullException(nameof(bytes));
            var charCount = (int)Math.Ceiling(bytes.Length / 5d) * 8;
            var returnArray = new char[charCount];
            byte nextChar = 0, bitsRemaining = 5;
            var arrayIndex = 0;
            foreach (var b in bytes)
            {
                nextChar = (byte)(nextChar | (b >> (8 - bitsRemaining)));
                returnArray[arrayIndex++] = ValueToChar(nextChar);
                if (bitsRemaining < 4)
                {
                    nextChar = (byte)((b >> (3 - bitsRemaining)) & 31);
                    returnArray[arrayIndex++] = ValueToChar(nextChar);
                    bitsRemaining += 5;
                }
                bitsRemaining -= 3;
                nextChar = (byte)((b << bitsRemaining) & 31);
            }
            // 如果没有以完整的字符结尾
            if (arrayIndex != charCount)
            {
                returnArray[arrayIndex++] = ValueToChar(nextChar);
                while (arrayIndex != charCount)
                    returnArray[arrayIndex++] = '='; // 填充
            }
            return new string(returnArray);
        }

        /// <summary>
        /// 转换为Base32字符串
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="encoding">字符编码</param>
        public static string ToBase32String(string str, Encoding encoding = null) => ToBase32String(encoding.Fixed().GetBytes(str));

        /// <summary>
        /// 将Base32字符串转换为字符串
        /// </summary>
        /// <param name="base32String">Base32字符串</param>
        /// <param name="encoding">字符编码</param>
        public static string FromBase32String(string base32String, Encoding encoding = null) => encoding.Fixed().GetString(FromBase32StringToBytes(base32String));

        /// <summary>
        /// 将Base32字符串转换为字节数组
        /// </summary>
        /// <param name="base32String">Base32字符串</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static byte[] FromBase32StringToBytes(string base32String)
        {
            if (base32String is null)
                throw new ArgumentNullException(nameof(base32String));
            base32String = base32String.TrimEnd('=');// 移除填充字符
            var byteCount = base32String.Length * 5 / 8;// 进行截断
            var returnArray = new byte[byteCount];

            byte curByte = 0, bitsRemaining = 8;
            var arrayIndex = 0;
            foreach (var c in base32String)
            {
                var cValue = CharToValue(c);
                int mask;

                if (bitsRemaining > 5)
                {
                    mask = cValue << (bitsRemaining - 5);
                    curByte = (byte)(curByte | mask);
                    bitsRemaining -= 5;
                }
                else
                {
                    mask = cValue >> (5 - bitsRemaining);
                    curByte = (byte)(curByte | mask);
                    returnArray[arrayIndex++] = curByte;
                    curByte = (byte)(cValue << (3 + bitsRemaining));
                    bitsRemaining += 3;
                }
            }
            // 如果没有以完整的字符结尾
            if (arrayIndex != byteCount)
                returnArray[arrayIndex] = curByte;
            return returnArray;
        }

        /// <summary>
        /// 字符转换为数值
        /// </summary>
        /// <param name="c">字符</param>
        private static int CharToValue(char c)
        {
            var value = (int)c;
            // 65-90 == 大写字母
            if (value < 91 && value > 64)
                return value - 65;
            // 50-55 == 数字 2-7
            if (value < 56 && value > 49)
                return value - 24;
            // 97-122 == 小写字母
            if (value < 123 && value > 96)
                return value - 97;
            throw new ArgumentException("Character is not a Base32 character.", nameof(c));
        }

        /// <summary>
        /// 数值转换为字符
        /// </summary>
        /// <param name="b">数值</param>
        private static char ValueToChar(byte b)
        {
            if (b < 26)
                return (char)(b + 65);
            if (b < 32)
                return (char)(b + 24);
            throw new ArgumentException("Byte is not a Base32 value.", nameof(b));
        }

        /// <summary>
        /// 补全。如果编码为空，则默认返回<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="encoding">编码</param>
        private static Encoding Fixed(this Encoding encoding) => encoding ?? Encoding.UTF8;
    }
}
