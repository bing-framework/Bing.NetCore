using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展 - 转换
    /// </summary>
    public static partial class StringExtensions
    {
        #region ToBytes(转换成Byte[])

        /// <summary>
        /// 将字符串转为byte[]数组，默认编码为<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="value">字符串</param>
        /// <param name="encoding">编码格式</param>
        public static byte[] ToBytes(this string value, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            return encoding.GetBytes(value);
        }

        #endregion

        #region ToXDocument(转换成XDocument)

        /// <summary>
        /// 字符串转为XDocument（Linq to Xml Dom）
        /// </summary>
        /// <param name="xml">xml字符串</param>
        public static XDocument ToXDocument(this string xml) => XDocument.Parse(xml);

        #endregion

        #region ToXElement(转换成XElement)

        /// <summary>
        /// 字符串转为XElement对象（Linq to Xml XElement）
        /// </summary>
        /// <param name="xml">xml字符串</param>
        public static XElement ToXElement(this string xml) => XElement.Parse(xml);

        #endregion

        #region ToXmlDocument(转换成XmlDocument)

        /// <summary>
        /// 字符串转为XmlDocument对象（Xml Dom）
        /// </summary>
        /// <param name="xml">xml字符串</param>
        public static XmlDocument ToXmlDocument(this string xml)
        {
            var document = new XmlDocument();
            document.LoadXml(xml);
            return document;
        }

        #endregion

        #region ToXPath(转换成XPath)

        /// <summary>
        /// 字符串转为XmlPathDom对象（Xml XPath Dom）
        /// </summary>
        /// <param name="xml">xml字符串</param>
        public static XPathNavigator ToXPath(this string xml)
        {
            var document = new XPathDocument(new StringReader(xml));
            return document.CreateNavigator();
        }

        #endregion



        #region HexStringToBytes(16进制字符串转换成字节数组)

        /// <summary>
        /// 16进制字符串转换为字节数组
        /// </summary>
        /// <param name="value">16进制字符串</param>
        public static byte[] HexStringToBytes(this string value)
        {
            value = value.Replace(" ", "");
            var maxByte = value.Length / 2 - 1;
            var bytes = new byte[maxByte + 1];
            for (var i = 0; i <= maxByte; i++)
                bytes[i] = byte.Parse(value.Substring(2 * i, 2), NumberStyles.AllowHexSpecifier);
            return bytes;
        }

        #endregion

        #region ToUnicodeString(转换成Unicode字符串)

        /// <summary>
        /// 转换成Unicode字符串
        /// </summary>
        /// <param name="source">源字符串</param>
        public static string ToUnicodeString(this string source)
        {
            string outString = "";
            if (!string.IsNullOrEmpty(source))
                outString = source.Aggregate(outString, (current, t) => current + (@"\u" + ((int)t).ToString("x").ToUpper()));
            return outString;
        }

        #endregion

        #region ToSecureString(转换成安全字符串)

        /// <summary>
        /// 将字符串转为安全字符串
        /// </summary>
        /// <param name="value">普通字符串</param>
        /// <param name="markReadOnly">是否只读</param>
        public static SecureString ToSecureString(this string value, bool markReadOnly = true)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var temp = new SecureString();
            foreach (var c in value)
                temp.AppendChar(c);
            if (markReadOnly)
                temp.MakeReadOnly();
            return temp;
        }

        #endregion

        #region ToUnSecureString(转换成普通字符串)

        /// <summary>
        /// 将安全字符串转为普通字符串
        /// </summary>
        /// <param name="value">安全字符串</param>
        public static string ToUnSecureString(this SecureString value)
        {
            if (ReferenceEquals(value, null))
                return null;
            var unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }

        #endregion

        #region ToSbcCase(转换成全角)

        /// <summary>
        /// 将字符串转换成全角字符串(SBC Case)
        /// </summary>
        /// <param name="input">任意字符串</param>
        public static string ToSbcCase(this string input)
        {
            var c = input.ToCharArray();
            for (var i = 0; i < c.Length; i++)
            {
                if (c[i] == 32)
                {
                    c[i] = (char)12288;
                    continue;
                }
                if (c[i] < 127)
                {
                    c[i] = (char)(c[i] + 65248);
                }
            }
            return new string(c);
        }

        #endregion

        #region ToDbcCase(转换成半角)

        /// <summary>
        /// 将字符串转换成半角字符串(DBC Case)
        /// </summary>
        /// <param name="input">任意字符串</param>
        public static string ToDbcCase(this string input)
        {
            var c = input.ToCharArray();
            for (var i = 0; i < c.Length; i++)
            {
                if (c[i] == 12288)
                {
                    c[i] = (char)32;
                    continue;
                }
                if (c[i] > 35280 && c[i] < 65375)
                {
                    c[i] = (char)(c[i] - 65248);
                }
            }
            return new string(c);
        }

        #endregion

        #region ToDateTime(时间戳转换成时间)

        /// <summary>
        /// 将时间戳转换成时间
        /// </summary>
        /// <param name="timeStamp">时间戳格式字符串</param>
        public static DateTime ToDateTime(this string timeStamp)
        {
            if (timeStamp.Length > 10)
                timeStamp = timeStamp.Substring(0, 10);
            var dateTimeStart = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            var lIime = long.Parse(timeStamp + "0000000");
            var toNow = new TimeSpan(lIime);
            return dateTimeStart.Add(toNow);
        }

        #endregion
    }
}
