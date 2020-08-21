using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace Bing.Helpers
{
    /// <summary>
    /// Url操作
    /// </summary>
    public static partial class Url
    {
        #region Combine(合并Url)

        /// <summary>
        /// 合并Url
        /// </summary>
        /// <param name="urls">url片段，范例：Url.Combine( "http://a.com","b" ),返回 "http://a.com/b"</param>
        public static string Combine(params string[] urls) => Path.Combine(urls).Replace(@"\", "/");

        #endregion

        #region Join(连接Url)

        /// <summary>
        /// 连接Url，范例：Url.Join( "http://a.com","b=1" ),返回 "http://a.com?b=1"
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        /// <param name="param">参数，范例：b=1</param>
        public static string Join(string url, string param) => $"{GetUrl(url)}{param}";

        /// <summary>
        /// 连接Url，范例：Url.Join( "http://a.com",new []{"b=1","c=2"})，返回"http://a.com?b=1&amp;c=2"
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        /// <param name="parameters">参数，范例：b=1</param>
        public static string Join(string url, params string[] parameters)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentNullException(nameof(url));
            if (parameters.Length == 0)
                return url;
            var currentUrl = Join(url, parameters[0]);
            return Join(currentUrl, parameters.Skip(1).ToArray());
        }

        /// <summary>
        /// 获取Url
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        private static string GetUrl(string url)
        {
            if (!url.Contains("?"))
                return $"{url}?";
            if (url.EndsWith("?"))
                return url;
            if (url.EndsWith("&"))
                return url;
            return $"{url}&";
        }

        /// <summary>
        /// 连接Url，范例：Url.Join( "http://a.com","b=1" ),返回 "http://a.com?b=1"
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        /// <param name="param">参数，范例：b=1</param>
        public static Uri Join(Uri url, string param)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            return new Uri(Join(url.AbsoluteUri, param));
        }

        /// <summary>
        /// 连接Url，范例：Url.Join( "http://a.com",new []{"b=1","c=2"})，返回"http://a.com?b=1&amp;c=2"
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        /// <param name="parameters">参数，范例：b=1</param>
        public static Uri Join(Uri url, params string[] parameters)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            return new Uri(Join(url.AbsoluteUri, parameters));
        }

        #endregion

        #region GetMainDomain(获取主域名)

        /// <summary>
        /// 获取主域名
        /// </summary>
        /// <param name="url">Url地址</param>
        public static string GetMainDomain(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;
            var array = url.Split('.');
            if (array.Length != 3)
                return url;
            var tok = new List<string>(array);
            var remove = array.Length - 2;
            tok.RemoveRange(0, remove);
            return tok[0] + "." + tok[1];
        }

        #endregion

        #region UrlEncode(Url编码)

        /// <summary>
        /// Url编码
        /// </summary>
        /// <param name="url">url</param>
        /// <param name="isUpper">编码字符是否转成大写，范例："http://"转成"http%3A%2F%2F"</param>
        public static string UrlEncode(string url, bool isUpper = false) => UrlEncode(url, Encoding.UTF8, isUpper);

        /// <summary>
        /// Url编码
        /// </summary>
        /// <param name="url">url</param>
        /// <param name="encoding">字符编码</param>
        /// <param name="isUpper">编码字符是否转成大写，范例："http://"转成"http%3A%2F%2F"</param>
        public static string UrlEncode(string url, string encoding, bool isUpper = false)
        {
            encoding = string.IsNullOrWhiteSpace(encoding) ? "UTF-8" : encoding;
            return UrlEncode(url, Encoding.GetEncoding(encoding), isUpper);
        }

        /// <summary>
        /// Url编码
        /// </summary>
        /// <param name="url">url</param>
        /// <param name="encoding">字符编码</param>
        /// <param name="isUpper">编码字符是否转成大写，范例："http://"转成"http%3A%2F%2F"</param>
        public static string UrlEncode(string url, Encoding encoding, bool isUpper = false)
        {
            var result = HttpUtility.UrlEncode(url, encoding);
            return isUpper == false ? result : GetUpperEncode(result);
        }

        /// <summary>
        /// 获取大写编码字符串
        /// </summary>
        /// <param name="encode">编码字符串</param>
        private static string GetUpperEncode(string encode)
        {
            var result = new StringBuilder();
            var index = int.MinValue;
            for (var i = 0; i < encode.Length; i++)
            {
                var character = encode[i].ToString();
                if (character == "%")
                    index = i;
                if (i - index == 1 || i - index == 2)
                    character = character.ToUpper();
                result.Append(character);
            }
            return result.ToString();
        }

        #endregion

        #region UrlDecode(Url解码)

        /// <summary>
        /// Url解码
        /// </summary>
        /// <param name="url">url</param>
        public static string UrlDecode(string url) => HttpUtility.UrlDecode(url);

        /// <summary>
        /// Url解码
        /// </summary>
        /// <param name="url">url</param>
        /// <param name="encoding">字符编码</param>
        public static string UrlDecode(string url, Encoding encoding) => HttpUtility.UrlDecode(url, encoding);

        #endregion
    }
}
