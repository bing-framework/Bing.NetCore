using System;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.ExceptionHandling
{
    /// <summary>
    /// Http异常状态码查找器
    /// </summary>
    public interface IHttpExceptionStatusCodeFinder
    {
        /// <summary>
        /// 获取状态码
        /// </summary>
        /// <param name="httpContext">Http上下文</param>
        /// <param name="exception">异常</param>
        HttpStatusCode GetStatusCode(HttpContext httpContext, Exception exception);
    }
}
