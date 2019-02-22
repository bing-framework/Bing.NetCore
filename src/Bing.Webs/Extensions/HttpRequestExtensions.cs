using System;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Http;

namespace Bing.Webs.Extensions
{
    /// <summary>
    /// Http请求(<see cref="HttpRequest"/>) 扩展
    /// </summary>
    public static class HttpRequestExtensions
    {
        /// <summary>
        /// 是否Ajax请求
        /// </summary>
        /// <param name="request">Http请求</param>
        /// <returns></returns>
        public static bool IsAjaxRequest(this HttpRequest request)
        {
            request.CheckNotNull(nameof(request));
            bool? flag = request.Headers?["X-Requested-With"].ToString()
                ?.Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            return flag.HasValue && flag.Value;
        }

        /// <summary>
        /// 是否Json内容类型
        /// </summary>
        /// <param name="request">Http请求</param>
        /// <returns></returns>
        public static bool IsJsonContentType(this HttpRequest request)
        {
            request.CheckNotNull(nameof(request));
            bool flag =
                request.Headers?["Content-Type"].ToString()
                    .IndexOf("application/json", StringComparison.OrdinalIgnoreCase) > -1 || request
                    .Headers?["Content-Type"].ToString().IndexOf("text/json", StringComparison.OrdinalIgnoreCase) > -1;

            if (flag)
            {
                return true;
            }

            flag =
                request.Headers?["Accept"].ToString().IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >
                -1 || request.Headers?["Accept"].ToString().IndexOf("text/json", StringComparison.OrdinalIgnoreCase) >
                -1;
            return flag;
        }

        /// <summary>
        /// 获取<see cref="HttpRequest" />的请求数据
        /// </summary>
        /// <param name="request">请求信息</param>
        /// <param name="key">键名</param>
        /// <returns></returns>
        public static string Params(this HttpRequest request, string key)
        {
            if (request.Query.ContainsKey(key))
            {
                return request.Query[key];
            }

            if (request.HasFormContentType)
            {
                return request.Form[key];
            }

            return null;
        }
    }
}
