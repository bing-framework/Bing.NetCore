using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Logs
{
    /// <summary>
    /// 请求响应记录器 操作过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequestResponseLoggerActionFilter : Attribute, IActionFilter
    {
        /// <summary>
        /// 获取请求响应日志
        /// </summary>
        /// <param name="context">Http上下文</param>
        private RequestResponseLog GetLog(HttpContext context)
        {
            return context.RequestServices.GetRequiredService<IRequestResponseLogCreator>().Log;
        }

        /// <summary>
        /// 执行操作之前。在模型绑定完成之后，在执行操作之前调用。
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var log = GetLog(context.HttpContext);
            log.RequestDateTimeUtcActionLevel = DateTime.UtcNow;
        }

        /// <summary>
        /// 执行操作之后。在操作执行之后，操作结果执行之前调用。
        /// </summary>
        /// <param name="context">已执行操作上下文</param>
        public void OnActionExecuted(ActionExecutedContext context)
        {
            var log = GetLog(context.HttpContext);
            log.ResponseDateTimeUtcActionLevel = DateTime.UtcNow;
        }
    }
}
