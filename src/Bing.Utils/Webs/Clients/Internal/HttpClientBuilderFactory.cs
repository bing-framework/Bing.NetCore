using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Bing.Utils.Webs.Clients.Internal
{
    /// <summary>
    /// HttpClien生成工厂
    /// </summary>
    internal static class HttpClientBuilderFactory
    {
        /// <summary>
        /// HttpClient 字典
        /// </summary>
        private static readonly IDictionary<string, HttpClient> _httpClients =
            new ConcurrentDictionary<string, HttpClient>();

        /// <summary>
        /// 域名正则表达式
        /// </summary>
        private static readonly Regex _domainRegex =
            new Regex(@"(http|https)://(?<domain>[^(:|/]*)", RegexOptions.IgnoreCase);

        /// <summary>
        /// 创建Http客户端
        /// </summary>
        /// <param name="url">请求地址</param>
        /// <param name="timeout">超时时间</param>
        /// <returns></returns>
        public static HttpClient CreateClient(string url,TimeSpan timeout)
        {
            var domain = GetDomainByUrl(url);
            if (_httpClients.ContainsKey(domain))
            {
                return _httpClients[domain];
            }

            var httpClient = Create(timeout);
            _httpClients[domain] = httpClient;
            return httpClient;
        }

        /// <summary>
        /// 通过Url地址获取域名
        /// </summary>
        /// <param name="url">Url地址</param>
        /// <returns></returns>
        private static string GetDomainByUrl(string url)
        {
            return _domainRegex.Match(url).Value;
        }

        /// <summary>
        /// 创建Http客户端
        /// </summary>
        /// <returns></returns>
        private static HttpClient Create(TimeSpan timeout)
        {
            var httpClient = new HttpClient(new HttpClientHandler()
            {
                UseProxy = false,
            })
            {
                Timeout = timeout
            };
            //httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
            return httpClient;
        }
    }
}
