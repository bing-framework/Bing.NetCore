using System;
using System.Linq;
using Bing.Utils.Properties;

namespace Bing.Utils.Maths
{
    /// <summary>
    /// 进制转换
    /// </summary>
    public class HexConv
    {
        /// <summary>
        /// 基础字符
        /// </summary>
        private const string BASE_CHAR = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// 二进制转换为八进制
        /// </summary>
        /// <param name="value">二进制</param>
        /// <returns></returns>
        public static string BinToOct(string value)
        {
            return X2X(value, 2, 8);
        }

        /// <summary>
        /// 二进制转换为十进制
        /// </summary>
        /// <param name="value">二进制</param>
        /// <returns></returns>
        public static string BinToDec(string value)
        {
            return X2X(value, 2, 10);
        }

        /// <summary>
        /// 二进制转换为十六进制
        /// </summary>
        /// <param name="value">二进制</param>
        /// <returns></returns>
        public static string BinToHex(string value)
        {
            return X2X(value, 2, 16);
        }

        /// <summary>
        /// 八进制转换为二进制
        /// </summary>
        /// <param name="value">八进制</param>
        /// <returns></returns>
        public static string OctToBin(string value)
        {
            return X2X(value, 8, 2);
        }

        /// <summary>
        /// 八进制转换为十进制
        /// </summary>
        /// <param name="value">八进制</param>
        /// <returns></returns>
        public static string OctToDec(string value)
        {
            return X2X(value, 8, 10);
        }

        /// <summary>
        /// 八进制转换为十六进制
        /// </summary>
        /// <param name="value">八进制</param>
        /// <returns></returns>
        public static string OctToHex(string value)
        {
            return X2X(value, 8, 16);
        }

        /// <summary>
        /// 十进制转换为二进制
        /// </summary>
        /// <param name="value">十进制</param>
        /// <returns></returns>
        public static string DecToBin(string value)
        {
            return X2X(value, 10, 2);
        }

        /// <summary>
        /// 十进制转换为八进制
        /// </summary>
        /// <param name="value">十进制</param>
        /// <returns></returns>
        public static string DecToOct(string value)
        {
            return X2X(value, 10, 8);
        }

        /// <summary>
        /// 十进制转换为十六进制
        /// </summary>
        /// <param name="value">十进制</param>
        /// <returns></returns>
        public static string DecToHex(string value)
        {
            return X2X(value, 10, 16);
        }

        /// <summary>
        /// 十六进制转换为二进制
        /// </summary>
        /// <param name="value">十六进制</param>
        /// <returns></returns>
        public static string HexToBin(string value)
        {
            return X2X(value, 16, 2);
        }

        /// <summary>
        /// 十六进制转换为八进制
        /// </summary>
        /// <param name="value">十六进制</param>
        /// <returns></returns>
        public static string HexToOct(string value)
        {
            return X2X(value, 16, 8);
        }

        /// <summary>
        /// 十六进制转换为十进制
        /// </summary>
        /// <param name="value">十六进制</param>
        /// <returns></returns>
        public static string HexToDec(string value)
        {
            return X2X(value, 16, 10);
        }

        /// <summary>
        /// 任意进制转换，将源进制表示的value转换为目标进制，进制的字符排序为先大写后小写
        /// </summary>
        /// <param name="value">要转换的数据</param>
        /// <param name="fromRadix">源进制数，必须为[2,62]范围内</param>
        /// <param name="toRadix">目标进制数，必须为[2,62]范围内</param>
        /// <returns></returns>
        public static string X2X(string value, int fromRadix, int toRadix)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (fromRadix < 2 || fromRadix > 62)
            {
                throw new ArgumentOutOfRangeException(nameof(fromRadix));
            }
            if (toRadix < 2 || toRadix > 62)
            {
                throw new ArgumentOutOfRangeException(nameof(toRadix));
            }

            ulong num = X2H(value, fromRadix);
            return H2X(num, toRadix);
        }

        /// <summary>
        /// 将64位有符号整数形式的数值转换为指定基数的数值的字符串形式
        /// </summary>
        /// <param name="value">64位有符号整数形式的数值</param>
        /// <param name="toRadix">要转换的目标基数，必须为[2,62]范围内</param>
        /// <returns>指定基数的数值的字符串形式</returns>
        public static string H2X(ulong value, int toRadix)
        {
            if (toRadix < 2 || toRadix > 62)
            {
                throw new ArgumentOutOfRangeException(nameof(toRadix));
            }

            if (value == 0)
            {
                return "0";
            }

            var baseChar = GetBaseChar(toRadix);

            string result = string.Empty;
            while (value > 0)
            {
                int index = (int)(value % (ulong)baseChar.Length);
                result = baseChar[index] + result;
                value = value / (ulong)baseChar.Length;
            }

            return result;
        }

        /// <summary>
        /// 将指定基数的数字的字符串表示形式转换为等效的64位有符号整数
        /// </summary>
        /// <param name="value">指定基数的数字的字符串表示</param>
        /// <param name="fromRadix">字符串的基数，必须为[2,62]范围内</param>
        /// <returns>等效于value的数值的64位有符号整数</returns>
        public static ulong X2H(string value, int fromRadix)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (fromRadix < 2 || fromRadix > 62)
            {
                throw new ArgumentOutOfRangeException(nameof(fromRadix));
            }

            value = value.Trim();
            var baseChar = GetBaseChar(fromRadix);
            ulong result = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char @char = value[i];
                if (!baseChar.Contains(@char))
                {
                    throw new ArgumentException(string.Format(R.AnyRadixConvert_CharacterIsNotValid, @char, fromRadix));
                }

                result += (ulong)baseChar.IndexOf(@char) * (ulong)Math.Pow(baseChar.Length, value.Length - i - 1);
            }

            return result;
        }

        /// <summary>
        /// 获取基础字符串
        /// </summary>
        /// <param name="radix">进制数</param>
        /// <returns></returns>
        private static string GetBaseChar(int radix)
        {
            var result = string.Empty;
            switch (radix)
            {
                case 26:
                    result = "abcdefghijklmnopqrstuvwxyz";
                    break;
                case 32:
                    result = "0123456789ABCDEFGHJKMNPQRSTVWXYZabcdefghijklmnopqrstuvwxyz";
                    break;
                case 36:
                    result = "0123456789abcdefghijklmnopqrstuvwxyz";
                    break;
                case 52:
                    result = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    break;
                case 58:
                    result = "123456789abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
                    break;
                case 62:
                    result = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    break;
                default:
                    result = BASE_CHAR;
                    break;
            }

            return result.Substring(0, radix);
        }
    }
}
