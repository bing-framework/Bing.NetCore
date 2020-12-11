using Bing.Exceptions;
using Bing.Helpers;
using Bing.Logs;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// 异常处理过滤器
    /// </summary>
    public class ExceptionHandlerAttribute : ExceptionFilterAttribute
    {
        /// <summary>
        /// 异常处理
        /// </summary>
        /// <param name="context">异常上下文</param>
        public override void OnException(ExceptionContext context)
        {
            context.ExceptionHandled = true;
            context.HttpContext.Response.StatusCode = 200;
            if (context.Exception is Warning warning)
            {
                context.Result = !string.IsNullOrWhiteSpace(warning.Code)
                    ? new ApiResult(Conv.ToInt(warning.Code), warning.GetPrompt())
                    : new ApiResult(StatusCode.Fail, warning.GetPrompt());
            }
            else
            {
                var log = Log.GetLog(context).Tag(nameof(ExceptionHandlerAttribute)).Caption("WebApi全局异常");
                context.Exception.GetRawException().Log(log);
                context.Result = new ApiResult(StatusCode.Fail, context.Exception.GetPrompt());
            }
        }
    }
}
