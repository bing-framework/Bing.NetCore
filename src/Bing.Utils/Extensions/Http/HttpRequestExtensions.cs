using System.Text;
using Microsoft.AspNetCore.Http;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// Http请求(<see cref="HttpRequest"/>) 扩展
    /// </summary>
    public static class HttpRequestExtensions
    {
        /// <summary>
        /// 获取Http请求的绝对路径
        /// </summary>
        /// <param name="request">Http请求</param>
        /// <returns></returns>
        public static string GetAbsoluteUri(this HttpRequest request) => new StringBuilder()
            .Append(request.Scheme)
            .Append("://")
            .Append(request.Host)
            .Append(request.PathBase)
            .Append(request.Path)
            .Append(request.QueryString)
            .ToString();
    }
}
