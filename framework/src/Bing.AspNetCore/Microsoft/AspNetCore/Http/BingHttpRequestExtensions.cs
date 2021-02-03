using Bing.Helpers;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// Http请求(<see cref="HttpRequest"/>) 扩展
    /// </summary>
    public static class BingHttpRequestExtensions
    {
        /// <summary>
        /// 请求头：X-Requested-With
        /// </summary>
        private const string RequestedWithHeader = "X-Requested-With";

        /// <summary>
        /// 请求头值：XMLHttpRequest
        /// </summary>
        private const string XmlHttpRequest = "XMLHttpRequest";

        /// <summary>
        /// 是否Ajax请求
        /// </summary>
        /// <param name="request">Http请求</param>
        public static bool IsAjax(this HttpRequest request)
        {
            Check.NotNull(request, nameof(request));
            if (request.Headers == null)
                return false;
            return request.Headers[RequestedWithHeader] == XmlHttpRequest;
        }

        /// <summary>
        /// 能否接收
        /// </summary>
        /// <param name="request">Http请求</param>
        /// <param name="contentType">内容类型</param>
        public static bool CanAccept(this HttpRequest request, string contentType)
        {
            Check.NotNull(request, nameof(request));
            Check.NotNull(contentType, nameof(contentType));
            return request.Headers["Accept"].ToString().Contains(contentType);
        }
    }
}
