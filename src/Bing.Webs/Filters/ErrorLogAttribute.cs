using System.Threading.Tasks;
using Bing.Logs;
using Bing.Logs.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.Webs.Filters
{
    /// <summary>
    /// 错误日志过滤器
    /// </summary>
    public class ErrorLogAttribute:ExceptionFilterAttribute
    {
        /// <summary>
        /// 异常处理
        /// </summary>
        /// <param name="context">异常上下文</param>
        public override void OnException(ExceptionContext context)
        {
            WriteLog(context);
        }

        /// <summary>
        /// 异常处理
        /// </summary>
        /// <param name="context">异常上下文</param>
        /// <returns></returns>
        public override Task OnExceptionAsync(ExceptionContext context)
        {
            WriteLog(context);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="context">异常上下文</param>
        private void WriteLog(ExceptionContext context)
        {
            if (context == null)
            {
                return;
            }
            var log = Log.GetLog(this).Caption("WebApi全局异常");
            log.Caption("WebApi全局异常");
            context.Exception.Log(log);
        }
    }
}
