using System;
using System.IO;
using System.Linq;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// Url操作
    /// </summary>
    public static class Url
    {
        /// <summary>
        /// 合并Url
        /// </summary>
        /// <param name="urls">url片段，范例：Url.Combine( "http://a.com","b" ),返回 "http://a.com/b"</param>
        /// <returns></returns>
        public static string Combine(params string[] urls)
        {
            return Path.Combine(urls).Replace(@"\", "/");
        }

        /// <summary>
        /// 连接Url，范例：Url.Join( "http://a.com","b=1" ),返回 "http://a.com?b=1"
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        /// <param name="param">参数，范例：b=1</param>
        /// <returns></returns>
        public static string Join(string url, string param)
        {
            return $"{GetUrl(url)}{param}";
        }

        /// <summary>
        /// 连接Url，范例：Url.Join( "http://a.com",new []{"b=1","c=2"})，返回"http://a.com?b=1&c=2"
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        /// <param name="parameters">参数，范例：b=1</param>
        /// <returns></returns>
        public static string Join(string url, params string[] parameters)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException(nameof(url));
            }
            if (parameters.Length == 0)
            {
                return url;
            }
            var currentUrl = Join(url, parameters[0]);
            return Join(currentUrl, parameters.Skip(1).ToArray());
        }

        /// <summary>
        /// 获取Url
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        /// <returns></returns>
        private static string GetUrl(string url)
        {
            if (!url.Contains("?"))
            {
                return $"{url}?";
            }

            if (url.EndsWith("?"))
            {
                return url;
            }

            if (url.EndsWith("&"))
            {
                return url;
            }

            return $"{url}&";
        }

        /// <summary>
        /// 连接Url，范例：Url.Join( "http://a.com","b=1" ),返回 "http://a.com?b=1"
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        /// <param name="param">参数，范例：b=1</param>
        /// <returns></returns>
        public static Uri Join(Uri url, string param)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }
            return new Uri(Join(url.AbsoluteUri, param));
        }

        /// <summary>
        /// 连接Url，范例：Url.Join( "http://a.com",new []{"b=1","c=2"})，返回"http://a.com?b=1&c=2"
        /// </summary>
        /// <param name="url">Url，范例：http://a.com</param>
        /// <param name="parameters">参数，范例：b=1</param>
        /// <returns></returns>
        public static Uri Join(Uri url, params string[] parameters)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }
            return new Uri(Join(url.AbsoluteUri, parameters));
        }

    }
}
