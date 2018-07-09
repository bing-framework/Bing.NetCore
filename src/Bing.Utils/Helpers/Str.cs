using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 字符串操作 - 工具
    /// </summary>
    public partial class Str
    {
        #region Join(将集合连接为带分隔符的字符串)
        /// <summary>
        /// 将集合连接为带分隔符的字符串
        /// </summary>
        /// <typeparam name="T">集合元素类型</typeparam>
        /// <param name="list">集合</param>
        /// <param name="quotes">引号，默认不带引号，范例：单引号"'"</param>
        /// <param name="separator">分隔符，默认使用逗号分隔</param>
        /// <returns></returns>
        public static string Join<T>(IEnumerable<T> list, string quotes = "", string separator = ",")
        {
            if (list == null)
            {
                return string.Empty;
            }
            var result = new StringBuilder();
            foreach (var each in list)
            {
                result.AppendFormat("{0}{1}{0}{2}", quotes, each, separator);
            }
            if (separator == "")
            {
                return result.ToString();
            }
            return result.ToString().TrimEnd(separator.ToCharArray());
        }

        #endregion

        #region ToUnicode(字符串转Unicode)

        /// <summary>
        /// 字符串转Unicode
        /// </summary>
        /// <param name="value">值</param>
        /// <returns>Unicode编码后的字符串</returns>
        public static string ToUnicode(string value)
        {
            var bytes = Encoding.Unicode.GetBytes(value);
            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i += 2)
            {
                sb.AppendFormat("\\u{0}{1}", bytes[i + 1].ToString("x").PadLeft(2, '0'),
                    bytes[i].ToString("x").PadLeft(2, '0'));
            }

            return sb.ToString();
        }

        #endregion

        #region ToUnicodeByCn(中文字符串转Unicode)

        /// <summary>
        /// 中文字符串转Unicode
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string ToUnicodeByCn(string value)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(value))
            {
                char[] chars = value.ToCharArray();
                for (int i = 0; i < value.Length; i++)
                {
                    // 将中文字符串转换为十进制整数，然后转为十六进制Unicode字符
                    sb.Append(Regex.IsMatch(chars[i].ToString(), "([\u4e00-\u9fa5])")
                        ? ToUnicode(chars[i].ToString())
                        : chars[i].ToString());
                }
            }

            return sb.ToString();
        }

        #endregion

        #region UnicodeToStr(Unicode转字符串)

        /// <summary>
        /// Unicode转字符串
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string UnicodeToStr(string value)
        {
            return new Regex(@"\\u([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled).Replace(value,
                x => Convert.ToChar(Convert.ToUInt16(x.Result("$1"), 16)).ToString());
        }

        #endregion

        #region PinYin(获取汉字的拼音简码)

        /// <summary>
        /// 获取汉字的拼音简码，即首字母缩写。范例：中国，返回zg
        /// </summary>
        /// <param name="chineseText">汉字文本。范例： 中国</param>
        /// <returns>首字母缩写</returns>
        public static string PinYin(string chineseText)
        {
            if (string.IsNullOrWhiteSpace(chineseText))
            {
                return string.Empty;
            }
            var result=new StringBuilder();
            foreach (var text in chineseText)
            {
                result.AppendFormat("{0}", ResolvePinYin(text));
            }

            return result.ToString().ToLower();
        }

        /// <summary>
        /// 解析单个汉字的拼音简码
        /// </summary>
        /// <param name="text">汉字</param>
        /// <returns></returns>
        private static string ResolvePinYin(char text)
        {
            byte[] charBytes = Encoding.Default.GetBytes(text.ToString());
            if (charBytes[0] < 127)
            {
                return text.ToString();
            }
            var unicode = (ushort)(charBytes[0] * 256 + charBytes[1]);
            string pinYin = ResolveByCode(unicode);
            if (!string.IsNullOrWhiteSpace(pinYin))
            {
                return pinYin;
            }
            return ResolveByConst(text.ToString());
        }

        /// <summary>
        /// 使用字符编码方式获取拼音简码
        /// </summary>
        /// <param name="unicode">字符编码</param>
        /// <returns></returns>
        private static string ResolveByCode(ushort unicode)
        {
            if (unicode >= '\uB0A1' && unicode <= '\uB0C4')
            {
                return "A";
            }
            if (unicode >= '\uB0C5' && unicode <= '\uB2C0' && unicode != 45464)
            {
                return "B";
            }
            if (unicode >= '\uB2C1' && unicode <= '\uB4ED')
            {
                return "C";
            }
            if (unicode >= '\uB4EE' && unicode <= '\uB6E9')
            {
                return "D";
            }
            if (unicode >= '\uB6EA' && unicode <= '\uB7A1')
            {
                return "E";
            }
            if (unicode >= '\uB7A2' && unicode <= '\uB8C0')
            {
                return "F";
            }
            if (unicode >= '\uB8C1' && unicode <= '\uB9FD')
            {
                return "G";
            }
            if (unicode >= '\uB9FE' && unicode <= '\uBBF6')
            {
                return "H";
            }
            if (unicode >= '\uBBF7' && unicode <= '\uBFA5')
            {
                return "J";
            }
            if (unicode >= '\uBFA6' && unicode <= '\uC0AB')
            {
                return "K";
            }
            if (unicode >= '\uC0AC' && unicode <= '\uC2E7')
            {
                return "L";
            }
            if (unicode >= '\uC2E8' && unicode <= '\uC4C2')
            {
                return "M";
            }
            if (unicode >= '\uC4C3' && unicode <= '\uC5B5')
            {
                return "N";
            }
            if (unicode >= '\uC5B6' && unicode <= '\uC5BD')
            {
                return "O";
            }
            if (unicode >= '\uC5BE' && unicode <= '\uC6D9')
            {
                return "P";
            }
            if (unicode >= '\uC6DA' && unicode <= '\uC8BA')
            {
                return "Q";
            }
            if (unicode >= '\uC8BB' && unicode <= '\uC8F5')
            {
                return "R";
            }
            if (unicode >= '\uC8F6' && unicode <= '\uCBF9')
            {
                return "S";
            }
            if (unicode >= '\uCBFA' && unicode <= '\uCDD9')
            {
                return "T";
            }
            if (unicode >= '\uCDDA' && unicode <= '\uCEF3')
            {
                return "W";
            }
            if (unicode >= '\uCEF4' && unicode <= '\uD188')
            {
                return "X";
            }
            if (unicode >= '\uD1B9' && unicode <= '\uD4D0')
            {
                return "Y";
            }
            if (unicode >= '\uD4D1' && unicode <= '\uD7F9')
            {
                return "Z";
            }
            return string.Empty;
        }

        /// <summary>
        /// 通过拼音简码常量获取
        /// </summary>
        /// <param name="text">文本</param>
        /// <returns></returns>
        private static string ResolveByConst(string text)
        {
            int index = Const.ChinesePinYin.IndexOf(text, StringComparison.Ordinal);
            if (index < 0)
            {
                return string.Empty;
            }

            return Const.ChinesePinYin.Substring(index + 1, 1);
        }

        #endregion

        #region FirstLowerCase(首字母小写)

        /// <summary>
        /// 首字母小写
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string FirstLowerCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return $"{value.Substring(0, 1).ToLower()}{value.Substring(1)}";
        }

        #endregion
    }
}
