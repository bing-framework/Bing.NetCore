using System.Threading.Tasks;
using Bing.DependencyInjection;
using Bing.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.AspNetCore.Mvc.ExceptionHandling
{
    /// <summary>
    /// 异常过滤器
    /// </summary>
    public class BingExceptionFilter : IAsyncExceptionFilter, ITransientDependency
    {
        /// <summary>
        /// 异常处理
        /// </summary>
        /// <param name="context">异常上下文</param>
        public async Task OnExceptionAsync(ExceptionContext context)
        {
            if (!ShouldHandleException(context))
                return;
            await HandleAndWrapException(context);
        }

        /// <summary>
        /// 是否应该处理异常
        /// </summary>
        /// <param name="context">异常上下文</param>
        protected virtual bool ShouldHandleException(ExceptionContext context)
        {
            if (context.ActionDescriptor.IsControllerAction() && context.ActionDescriptor.HasObjectResult())
                return true;
            if (context.HttpContext.Request.CanAccept("application/json"))
                return true;
            if (context.HttpContext.Request.IsAjax())
                return true;
            return false;
        }

        /// <summary>
        /// 处理并包装异常
        /// </summary>
        /// <param name="context">异常上下文</param>
        protected virtual async Task HandleAndWrapException(ExceptionContext context)
        {
            await context.GetRequiredService<IExceptionNotifier>().NotifyAsync(new ExceptionNotificationContext(context.Exception));
            context.Exception = null;
        }
    }
}
