using System.Threading.Tasks;
using Bing.Authorization;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.ExceptionHandling
{
    /// <summary>
    /// 授权异常处理器
    /// </summary>
    public interface IBingAuthorizationExceptionHandler
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="exception">授权异常</param>
        /// <param name="httpContext">Http上下文</param>
        Task HandleAsync(BingAuthorizationException exception, HttpContext httpContext);
    }
}
