using System;
using Bing.Helpers;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Http请求(<see cref="HttpRequest"/>) 扩展
/// </summary>
public static class BingHttpRequestExtensions
{
    /// <summary>
    /// 请求头：X-Requested-With
    /// </summary>
    private const string XRequestedWith = "X-Requested-With";

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
        return string.Equals(request.Query[XRequestedWith], XmlHttpRequest, StringComparison.Ordinal) ||
               string.Equals(request.Headers[XRequestedWith], XmlHttpRequest, StringComparison.Ordinal);
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
#if NETCOREAPP3_0 || NETCOREAPP3_1 || NET5_0
            return request.Headers[HeaderNames.Accept].ToString().Contains(contentType, StringComparison.OrdinalIgnoreCase);
#elif NETSTANDARD2_0
        return request.Headers[HeaderNames.Accept].ToString().Contains(contentType);
#endif
    }
}