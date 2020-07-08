using System;
using System.Threading.Tasks;
using Bing.Logs;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.ExceptionHandling
{
    /// <summary>
    /// 异常处理中间件
    /// </summary>
    public class BingExceptionHandlingMiddleware : IMiddleware
    {
        /// <summary>
        /// 方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 初始化一个<see cref="BingExceptionHandlingMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        public BingExceptionHandlingMiddleware(RequestDelegate next) => _next = next;

        /// <summary>
        /// 执行中间件拦截逻辑
        /// </summary>
        /// <param name="context">Http上下文</param>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception e)
            {
                WriteLog(context, e);
                throw;
            }
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="context">Http上下文</param>
        /// <param name="ex">异常</param>
        private void WriteLog(HttpContext context, Exception ex)
        {
            if (context == null)
                return;
            var log = (ILog)context.RequestServices.GetService(typeof(ILog));
            log
                .Tag(nameof(BingExceptionHandlingMiddleware))
                .Caption("全局异常捕获 - 错误日志中间件")
                .Content($"状态码：{context.Response.StatusCode}");
            ex.Log(log);
        }
    }
}
