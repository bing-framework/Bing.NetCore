using Bing.Authorization;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// 授权异常处理器
/// </summary>
public interface IBingAuthorizationExceptionHandler
{
    /// <summary>
    /// 处理授权异常并写入 HTTP 响应。
    /// </summary>
    /// <param name="exception">授权异常</param>
    /// <param name="httpContext">Http上下文</param>
    Task HandleAsync(BingAuthorizationException exception, HttpContext httpContext);
}